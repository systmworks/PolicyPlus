using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Microsoft.Win32;
using PolicyPlus.ViewModels;
using Wpf.Ui.Controls;

namespace PolicyPlus.Views
{
    public partial class MainWindow : FluentWindow
    {
        private ConfigurationStorage _configuration;
        private AdmxBundle _admxWorkspace = new();
        private IPolicySource _userPolicySource, _compPolicySource;
        private PolicyLoader _userPolicyLoader, _compPolicyLoader;
        private Dictionary<string, string> _userComments, _compComments;
        private PolicyPlusCategory _currentCategory;
        private PolicyPlusPolicy _currentSetting;
        private FilterConfiguration _currentFilter = new();
        private PolicyPlusCategory _highlightCategory;
        private readonly Dictionary<PolicyPlusCategory, CategoryNodeViewModel> _categoryNodes = new();
        private bool _viewEmptyCategories;
        private AdmxPolicySection _viewPolicyTypes = AdmxPolicySection.Both;
        private bool _viewFilteredOnly;
        private bool _isDirty;
        private List<string> _favoriteIds = new();
        private CategoryNodeViewModel _favoritesNode;
        private CategoryNodeViewModel _selectedTreeNode;
        // Tracks whether Favorites is the active selection independent of node identity - the
        // CategoryNodeViewModel instances (including _favoritesNode itself) are all recreated on
        // every PopulateAdmxUi() rebuild, so comparing _selectedTreeNode against _favoritesNode by
        // reference always mismatches after a reload and silently emptied the Favorites listing.
        private bool _favoritesSelected;
        // Re-entrancy guard: setting a CategoryNodeViewModel's IsSelected (bound TwoWay to the real
        // TreeViewItem) synchronously re-fires SelectedItemChanged, which would otherwise re-enter
        // this method mid-update and redo the same work (visible flicker, wasted work; several UI
        // actions navigate to a category by setting _currentCategory directly and calling this
        // method, which itself drives tree selection as a side effect at the end).
        private bool _isUpdatingCategoryListing;
        private Func<PolicyPlusPolicy, bool> _searchMatcher;
        private int _sortColumn = -1;
        private bool _sortAscending = true;

        private ImageSource[] _icons;
        private ImageSource _prefWarningIcon;
        private readonly ObservableCollection<CategoryNodeViewModel> _treeRoot = new();
        private List<PolicyRowViewModel> _policyRows = new();

        public MainWindow()
        {
            InitializeComponent();
            LoadIcons();
            CategoriesTree.ItemsSource = _treeRoot;
        }

        // Icons live as individual PNGs under Resources/Icons (icon_00.png..icon_42.png, plus
        // pref_warning.png), embedded as WPF pack resources - loaded once here and shared as
        // ImageSource[] with this window's own TreeView/ListView rendering, plus
        // InspectPolicyElementsWindow/FindByIdWindow/EditPolWindow.
        private const int IconCount = 43;

        private void LoadIcons()
        {
            _icons = new ImageSource[IconCount];
            for (int i = 0; i < _icons.Length; i++)
                _icons[i] = LoadIcon($"icon_{i:D2}.png");
            _prefWarningIcon = LoadIcon("pref_warning.png");
            PolicyIsPrefIcon.Source = _prefWarningIcon;
        }

        private static ImageSource LoadIcon(string fileName)
        {
            var image = new BitmapImage(new Uri($"pack://application:,,,/Resources/Icons/{fileName}"));
            image.Freeze();
            return image;
        }

        private ImageSource Icon(int index) => index >= 0 && index < _icons.Length ? _icons[index] : null;

        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            _configuration = new ConfigurationStorage(RegistryHive.CurrentUser, @"Software\Policy Plus");
            Title = $"Policy Plus {VersionHolder.AppVersion}";
            RestoreWindowBounds();
            RestorePaneLayout();
            SetColorModeMenuChecks(Convert.ToString(_configuration.GetValue("ColorMode", "System")));
            _favoriteIds = ((string[])_configuration.GetValue("Favorites", Array.Empty<string>())).ToList();

            OpenLastAdmxSource();
            var compLoaderType = (PolicyLoaderSource)Convert.ToInt32(_configuration.GetValue("CompSourceType", 0));
            var compLoaderData = _configuration.GetValue("CompSourceData", "");
            var userLoaderType = (PolicyLoaderSource)Convert.ToInt32(_configuration.GetValue("UserSourceType", 0));
            var userLoaderData = _configuration.GetValue("UserSourceData", "");
            try
            {
                OpenPolicyLoaders(new PolicyLoader(userLoaderType, Convert.ToString(userLoaderData), true), new PolicyLoader(compLoaderType, Convert.ToString(compLoaderData), false), true);
            }
            catch (Exception)
            {
                MsgBoxCompat.Show("The previous policy sources are not accessible. The defaults will be loaded.", MsgBoxButtons.OK, MsgBoxIcon.Warning);
                _configuration.SetValue("CompSourceType", (int)PolicyLoaderSource.LocalGpo);
                _configuration.SetValue("UserSourceType", (int)PolicyLoaderSource.LocalGpo);
                OpenPolicyLoaders(new PolicyLoader(PolicyLoaderSource.LocalGpo, "", true), new PolicyLoader(PolicyLoaderSource.LocalGpo, "", false), true);
            }

            PopulateAdmxUi();
            CheckAdmxAcquisitionPrompt();
        }

        private void CheckAdmxAcquisitionPrompt()
        {
            if (Convert.ToInt32(_configuration.GetValue("CheckedPolicyDefinitions", 0)) != 0)
                return;
            _configuration.SetValue("CheckedPolicyDefinitions", 1);
            if (!SystemInfo.HasGroupPolicyInfrastructure() && _admxWorkspace.Categories.Values.Where(c => IsOrphanCategory(c) && !IsEmptyCategory(c)).Count() > 2)
            {
                if (MsgBoxCompat.Show(
                    "Welcome to Policy Plus!" + Environment.NewLine + Environment.NewLine +
                    "Home editions do not come with the full set of policy definitions. Would you like to download them now? This can also be done later with Help | Acquire ADMX Files.",
                    MsgBoxButtons.YesNo, MsgBoxIcon.Information) == MsgBoxResult.Yes)
                {
                    AcquireAdmxFilesMenuItem_Click(null, null);
                }
            }
        }

        public void OpenLastAdmxSource()
        {
            string defaultAdmxSource = Environment.ExpandEnvironmentVariables(@"%windir%\PolicyDefinitions");
            string admxSource = Convert.ToString(_configuration.GetValue("AdmxSource", defaultAdmxSource));
            try
            {
                var fails = _admxWorkspace.LoadFolder(admxSource, GetPreferredLanguageCode());
                if (DisplayAdmxLoadErrorReport(fails, true) == MsgBoxResult.No)
                    throw new Exception("You decided to not use the problematic ADMX bundle.");
            }
            catch (Exception ex)
            {
                _admxWorkspace = new AdmxBundle();
                string loadFailReason = "";
                if ((admxSource ?? "") != (defaultAdmxSource ?? ""))
                {
                    if (MsgBoxCompat.Show("Policy definitions could not be loaded from \"" + admxSource + "\": " + ex.Message + Environment.NewLine + Environment.NewLine + "Load from the default location?", MsgBoxButtons.YesNo, MsgBoxIcon.Question) == MsgBoxResult.Yes)
                    {
                        try
                        {
                            _configuration.SetValue("AdmxSource", defaultAdmxSource);
                            _admxWorkspace = new AdmxBundle();
                            DisplayAdmxLoadErrorReport(_admxWorkspace.LoadFolder(defaultAdmxSource, GetPreferredLanguageCode()));
                        }
                        catch (Exception ex2)
                        {
                            loadFailReason = ex2.Message;
                        }
                    }
                }
                else
                {
                    loadFailReason = ex.Message;
                }

                if (!string.IsNullOrEmpty(loadFailReason))
                    MsgBoxCompat.Show("Policy definitions could not be loaded: " + loadFailReason, MsgBoxButtons.OK, MsgBoxIcon.Warning);
            }
        }

        public void PopulateAdmxUi()
        {
            _treeRoot.Clear();
            _categoryNodes.Clear();
            var visibilityCache = new Dictionary<PolicyPlusCategory, bool>();

            IEnumerable<CategoryNodeViewModel> buildLevel(IEnumerable<PolicyPlusCategory> categories) =>
                categories.Where(c => ShouldShowCategoryCore(c, visibilityCache))
                    .OrderBy(c => c.DisplayName, StringComparer.CurrentCulture)
                    .Select(category =>
                    {
                        var node = new CategoryNodeViewModel(category.DisplayName, category, Icon(GetImageIndexForCategory(category)))
                        {
                            SelectedIcon = Icon(3), // "Go" folder icon while selected
                        };
                        _categoryNodes.Add(category, node);
                        foreach (var child in buildLevel(category.Children))
                            node.Children.Add(child);
                        return node;
                    });

            foreach (var node in buildLevel(_admxWorkspace.Categories.Values))
                _treeRoot.Add(node);

            // Pinned at the top, not sorted alongside the real categories.
            _favoritesNode = new CategoryNodeViewModel("★ Favorites", null, Icon(0));
            _treeRoot.Insert(0, _favoritesNode);

            _currentCategory = null;
            UpdateCategoryListing();
            ClearSelections();
            UpdatePolicyInfo();
        }

        public void UpdateCategoryListing()
        {
            if (_isUpdatingCategoryListing)
                return;
            _isUpdatingCategoryListing = true;
            try
            {
                if (_favoritesSelected)
                {
                    UpdateFavoritesListing();
                    return;
                }

                bool inSameCategory = false;
                var rows = new List<PolicyRowViewModel>();
                if (_currentCategory is not null)
                {
                    if (_currentSetting is not null && ReferenceEquals(_currentSetting.Category, _currentCategory))
                        inSameCategory = true;
                    if (_currentCategory.Parent is not null)
                    {
                        rows.Add(new PolicyRowViewModel("Up: " + _currentCategory.Parent.DisplayName, "", "", "", Icon(6), _currentCategory.Parent, isUpRow: true));
                    }

                    foreach (var category in _currentCategory.Children.Where(ShouldShowCategory).OrderBy(c => c.DisplayName, StringComparer.CurrentCulture))
                        rows.Add(new PolicyRowViewModel(category.DisplayName, "", "", "", Icon(GetImageIndexForCategory(category)), category));
                    foreach (var policy in _currentCategory.Policies.Where(ShouldShowPolicy).OrderBy(p => p.DisplayName, StringComparer.CurrentCulture))
                        rows.Add(BuildPolicyRow(policy));

                    if (CategoryNodesContains(_currentCategory))
                        SelectTreeNode(_categoryNodes[_currentCategory]);
                }

                _ = inSameCategory; // Scroll-position preservation isn't replicated - see HANDOVER notes
                ApplySort(rows);
                _policyRows = rows;
                PoliciesList.ItemsSource = _policyRows;
                ReselectCurrentSetting();
            }
            finally
            {
                _isUpdatingCategoryListing = false;
            }
        }

        private bool CategoryNodesContains(PolicyPlusCategory category) => _categoryNodes.ContainsKey(category);

        private void UpdateFavoritesListing()
        {
            var rows = _favoriteIds
                .Select(id => _admxWorkspace.Policies.TryGetValue(id, out var policy) ? policy : null)
                .Where(p => p is not null)
                .OrderBy(p => p.DisplayName, StringComparer.CurrentCulture)
                .Select(BuildPolicyRow)
                .ToList();
            ApplySort(rows);
            _policyRows = rows;
            PoliciesList.ItemsSource = _policyRows;
            ReselectCurrentSetting();
        }

        private PolicyRowViewModel BuildPolicyRow(PolicyPlusPolicy policy) =>
            new(policy.DisplayName, GetPolicyState(policy), GetPolicyCommentText(policy), policy.UniqueID, Icon(GetImageIndexForSetting(policy)), policy);

        private void ReselectCurrentSetting()
        {
            if (_currentSetting is null)
                return;
            var row = _policyRows.FirstOrDefault(r => ReferenceEquals(r.Tag, _currentSetting));
            if (row is not null)
            {
                PoliciesList.SelectedItem = row;
                PoliciesList.ScrollIntoView(row);
            }
        }

        // Sorts by the current column's text, but only within the existing Up-row / category /
        // policy grouping (matching how the rows were just built), so sorting never interleaves
        // categories and policies.
        private void ApplySort(List<PolicyRowViewModel> rows)
        {
            if (_sortColumn < 0)
                return;
            int rank(PolicyRowViewModel r) => r.IsUpRow ? 0 : r.Tag is PolicyPlusCategory ? 1 : 2;
            string key(PolicyRowViewModel r) => _sortColumn switch
            {
                1 => r.State,
                2 => r.Comment,
                3 => r.Id,
                _ => r.Name,
            };
            var sorted = rows
                .Select((r, i) => (Row: r, Index: i))
                .OrderBy(x => rank(x.Row))
                .ThenBy(x => key(x.Row), StringComparer.CurrentCultureIgnoreCase)
                .ToList();
            if (!_sortAscending)
            {
                // Reverse within each rank group only, keeping Up/categories/policies separated.
                sorted = sorted.GroupBy(x => rank(x.Row)).SelectMany(g => g.Reverse()).ToList();
            }

            rows.Clear();
            rows.AddRange(sorted.Select(x => x.Row));
        }

        private void ColumnHeader_Click(object sender, RoutedEventArgs e)
        {
            int column = int.Parse((string)((GridViewColumnHeader)sender).Tag);
            if (_sortColumn == column)
                _sortAscending = !_sortAscending;
            else
            {
                _sortColumn = column;
                _sortAscending = true;
            }

            ApplySort(_policyRows);
            PoliciesList.ItemsSource = null;
            PoliciesList.ItemsSource = _policyRows;
            ReselectCurrentSetting();
            UpdateSortGlyphs();
        }

        private void UpdateSortGlyphs()
        {
            void setHeader(GridViewColumnHeader header, int column, string title)
            {
                if (column != _sortColumn)
                {
                    header.Content = title;
                    return;
                }

                header.Content = title + (_sortAscending ? " ▲" : " ▼");
            }

            setHeader(NameColumnHeader, 0, "Name");
            setHeader(StateColumnHeader, 1, "State");
            setHeader(CommentColumnHeader, 2, "Comment");
            setHeader(IdColumnHeader, 3, "ID");
        }

        public void UpdatePolicyInfo()
        {
            bool hasCurrentSetting = _currentSetting is not null || _highlightCategory is not null || _currentCategory is not null;
            PolicyTitleLabel.Visibility = hasCurrentSetting ? Visibility.Visible : Visibility.Collapsed;
            PolicySupportedLabel.Visibility = hasCurrentSetting ? Visibility.Visible : Visibility.Collapsed;
            if (_currentSetting is not null)
            {
                PolicyTitleLabel.Text = _currentSetting.DisplayName;
                PolicySupportedLabel.Text = _currentSetting.SupportedOn is null
                    ? "(no requirements information)"
                    : "Requirements:" + Environment.NewLine + _currentSetting.SupportedOn.DisplayName;
                PolicyDescLabel.Text = MainWindow.PrettifyDescription(_currentSetting.DisplayExplanation);
                PolicyIsPrefPanel.Visibility = IsPreference(_currentSetting) ? Visibility.Visible : Visibility.Collapsed;
            }
            else if (_highlightCategory is not null || _currentCategory is not null)
            {
                var shownCategory = _highlightCategory ?? _currentCategory;
                PolicyTitleLabel.Text = shownCategory.DisplayName;
                PolicySupportedLabel.Text = (_highlightCategory is null ? "This" : "The selected") + " category contains " + shownCategory.Policies.Count + " policies and " + shownCategory.Children.Count + " subcategories.";
                PolicyDescLabel.Text = MainWindow.PrettifyDescription(shownCategory.DisplayExplanation);
                PolicyIsPrefPanel.Visibility = Visibility.Collapsed;
            }
            else
            {
                PolicyDescLabel.Text = "Select an item to see its description.";
                PolicyIsPrefPanel.Visibility = Visibility.Collapsed;
            }
        }

        public bool IsOrphanCategory(PolicyPlusCategory category) => category.Parent is null && !string.IsNullOrEmpty(category.RawCategory.ParentID);

        public bool IsEmptyCategory(PolicyPlusCategory category) => category.Children.Count == 0 && category.Policies.Count == 0;

        public int GetImageIndexForCategory(PolicyPlusCategory category)
        {
            if (IsOrphanCategory(category))
                return 1;
            if (IsEmptyCategory(category))
                return 2;
            return 0;
        }

        public int GetImageIndexForSetting(PolicyPlusPolicy setting)
        {
            if (IsPreference(setting))
                return 7;
            if (setting.RawPolicy.Elements is null || setting.RawPolicy.Elements.Count == 0)
                return 4;
            return 5;
        }

        public bool ShouldShowCategory(PolicyPlusCategory category) => ShouldShowCategoryCore(category, null);

        private bool ShouldShowCategoryCore(PolicyPlusCategory category, Dictionary<PolicyPlusCategory, bool> cache)
        {
            if (_viewEmptyCategories)
                return true;
            if (cache is not null && cache.TryGetValue(category, out bool cached))
                return cached;
            bool result = category.Policies.Any(ShouldShowPolicy) || category.Children.Any(c => ShouldShowCategoryCore(c, cache));
            if (cache is not null)
                cache[category] = result;
            return result;
        }

        public bool ShouldShowPolicy(PolicyPlusPolicy policy)
        {
            if (!PolicyVisibleInSection(policy, _viewPolicyTypes))
                return false;
            if (_searchMatcher is not null && !_searchMatcher(policy))
                return false;
            if (_viewFilteredOnly)
            {
                if ((_viewPolicyTypes & AdmxPolicySection.Machine) > 0 && PolicyVisibleInSection(policy, AdmxPolicySection.Machine) && IsPolicyVisibleAfterFilter(policy, false))
                    return true;
                if ((_viewPolicyTypes & AdmxPolicySection.User) > 0 && PolicyVisibleInSection(policy, AdmxPolicySection.User) && IsPolicyVisibleAfterFilter(policy, true))
                    return true;
                return false;
            }

            return true;
        }

        public void MoveToVisibleCategoryAndReload()
        {
            var newFocusCategory = _currentCategory;
            var newFocusPolicy = _currentSetting;
            while (newFocusCategory is not null && !ShouldShowCategory(newFocusCategory))
            {
                newFocusCategory = newFocusCategory.Parent;
                newFocusPolicy = null;
            }

            if (newFocusPolicy is not null && !ShouldShowPolicy(newFocusPolicy))
                newFocusPolicy = null;
            PopulateAdmxUi();
            _currentCategory = newFocusCategory;
            UpdateCategoryListing();
            _currentSetting = newFocusPolicy;
            UpdatePolicyInfo();
        }

        public string GetPolicyState(PolicyPlusPolicy policy)
        {
            if (_viewPolicyTypes == AdmxPolicySection.Both)
            {
                string userState = GetPolicyState(policy, AdmxPolicySection.User);
                string machState = GetPolicyState(policy, AdmxPolicySection.Machine);
                var section = policy.RawPolicy.Section;
                if (section == AdmxPolicySection.Both)
                {
                    if ((userState ?? "") == (machState ?? ""))
                        return userState + " (2)";
                    if (userState == "Not Configured")
                        return machState + " (C)";
                    if (machState == "Not Configured")
                        return userState + " (U)";
                    return "Mixed";
                }

                return section == AdmxPolicySection.Machine ? machState + " (C)" : userState + " (U)";
            }

            return GetPolicyState(policy, _viewPolicyTypes);
        }

        public string GetPolicyState(PolicyPlusPolicy policy, AdmxPolicySection section) =>
            PolicyProcessing.GetPolicyState(section == AdmxPolicySection.Machine ? _compPolicySource : _userPolicySource, policy) switch
            {
                PolicyState.Disabled => "Disabled",
                PolicyState.Enabled => "Enabled",
                PolicyState.NotConfigured => "Not Configured",
                _ => "Unknown",
            };

        public string GetPolicyCommentText(PolicyPlusPolicy policy)
        {
            if (_viewPolicyTypes == AdmxPolicySection.Both)
            {
                string userComment = GetPolicyComment(policy, AdmxPolicySection.User);
                string compComment = GetPolicyComment(policy, AdmxPolicySection.Machine);
                if (string.IsNullOrEmpty(userComment) && string.IsNullOrEmpty(compComment))
                    return "";
                if (!string.IsNullOrEmpty(userComment) && !string.IsNullOrEmpty(compComment))
                    return "(multiple)";
                return !string.IsNullOrEmpty(userComment) ? userComment : compComment;
            }

            return GetPolicyComment(policy, _viewPolicyTypes);
        }

        public string GetPolicyComment(PolicyPlusPolicy policy, AdmxPolicySection section)
        {
            var commentSource = section == AdmxPolicySection.Machine ? _compComments : _userComments;
            if (commentSource is null)
                return "";
            return commentSource.TryGetValue(policy.UniqueID, out var comment) ? comment : "";
        }

        public bool IsPreference(PolicyPlusPolicy policy) =>
            !string.IsNullOrEmpty(policy.RawPolicy.RegistryKey) && !RegistryPolicyProxy.IsPolicyKey(policy.RawPolicy.RegistryKey);

        public void ShowSettingEditor(PolicyPlusPolicy policy, AdmxPolicySection section)
        {
            if (EditSettingWindow.PresentDialog(this, policy, section, _admxWorkspace, _compPolicySource, _userPolicySource, _compPolicyLoader, _userPolicyLoader, _compComments, _userComments))
            {
                _isDirty = true;
                if (_currentCategory is null || ShouldShowCategory(_currentCategory))
                    UpdateCategoryListing();
                else
                    MoveToVisibleCategoryAndReload();
            }
        }

        public void ClearSelections()
        {
            _currentSetting = null;
            _highlightCategory = null;
        }

        public void OpenPolicyLoaders(PolicyLoader user, PolicyLoader computer, bool quiet)
        {
            if (_compPolicyLoader is not null || _userPolicyLoader is not null)
                ClosePolicySources();
            _userPolicyLoader = user;
            _userPolicySource = user.OpenSource();
            _compPolicyLoader = computer;
            _compPolicySource = computer.OpenSource();
            bool allOk = true;
            string policyStatus(PolicyLoader loader)
            {
                switch (loader.GetWritability())
                {
                    case PolicySourceWritability.Writable:
                        return "is fully writable";
                    case PolicySourceWritability.NoCommit:
                        allOk = false;
                        return "cannot be saved";
                    default:
                        allOk = false;
                        return "cannot be modified";
                }
            }

            Dictionary<string, string> loadComments(PolicyLoader loader)
            {
                string cmtxPath = loader.GetCmtxPath();
                if (string.IsNullOrEmpty(cmtxPath))
                    return null;
                try
                {
                    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(cmtxPath));
                    return System.IO.File.Exists(cmtxPath) ? CmtxFile.Load(cmtxPath).ToCommentTable() : new Dictionary<string, string>();
                }
                catch (Exception)
                {
                    return null;
                }
            }

            string userStatus = policyStatus(user);
            string compStatus = policyStatus(computer);
            _userComments = loadComments(user);
            _compComments = loadComments(computer);
            UserSourceLabel.Text = _userPolicyLoader.GetDisplayInfo();
            ComputerSourceLabel.Text = _compPolicyLoader.GetDisplayInfo();
            if (allOk)
            {
                if (!quiet)
                    MsgBoxCompat.Show("Both the user and computer policy sources are loaded and writable.", MsgBoxButtons.OK, MsgBoxIcon.Information);
            }
            else
            {
                string msgText = "Not all policy sources are fully writable." + Environment.NewLine + Environment.NewLine + "The user source " + userStatus + "." + Environment.NewLine + Environment.NewLine + "The computer source " + compStatus + ".";
                MsgBoxCompat.Show(msgText, MsgBoxButtons.OK, MsgBoxIcon.Warning);
            }
        }

        public void ClosePolicySources()
        {
            bool allOk = true;
            if (_userPolicyLoader is not null && !_userPolicyLoader.Close())
                allOk = false;
            if (_compPolicyLoader is not null && !_compPolicyLoader.Close())
                allOk = false;
            if (!allOk)
                MsgBoxCompat.Show("Cleanup did not complete fully because the loaded resources are open in other programs.", MsgBoxButtons.OK, MsgBoxIcon.Warning);
        }

        public void ShowSearchDialog(Func<PolicyPlusPolicy, bool> searcher)
        {
            var selPol = searcher is null
                ? FindResultsWindow.PresentDialog(this)
                : FindResultsWindow.PresentDialogStartSearch(this, _admxWorkspace, searcher);
            if (selPol is not null)
            {
                ShowSettingEditor(selPol, _viewPolicyTypes);
                FocusPolicy(selPol);
            }
        }

        public void ClearAdmxWorkspace()
        {
            _admxWorkspace = new AdmxBundle();
            FindResultsWindow.ClearSearch();
        }

        public void FocusPolicy(PolicyPlusPolicy policy)
        {
            if (!_categoryNodes.ContainsKey(policy.Category))
                return;
            _currentCategory = policy.Category;
            UpdateCategoryListing();
            var row = _policyRows.FirstOrDefault(r => ReferenceEquals(r.Tag, policy));
            if (row is not null)
            {
                PoliciesList.SelectedItem = row;
                PoliciesList.ScrollIntoView(row);
            }
        }

        public bool IsPolicyVisibleAfterFilter(PolicyPlusPolicy policy, bool isUser)
        {
            if (_currentFilter.ManagedPolicy.HasValue && IsPreference(policy) == _currentFilter.ManagedPolicy.Value)
                return false;
            if (_currentFilter.PolicyState.HasValue)
            {
                var policyState = PolicyProcessing.GetPolicyState(isUser ? _userPolicySource : _compPolicySource, policy);
                switch (_currentFilter.PolicyState.Value)
                {
                    case FilterPolicyState.Configured:
                        if (policyState == PolicyState.NotConfigured)
                            return false;
                        break;
                    case FilterPolicyState.NotConfigured:
                        if (policyState != PolicyState.NotConfigured)
                            return false;
                        break;
                    case FilterPolicyState.Disabled:
                        if (policyState != PolicyState.Disabled)
                            return false;
                        break;
                    case FilterPolicyState.Enabled:
                        if (policyState != PolicyState.Enabled)
                            return false;
                        break;
                }
            }

            if (_currentFilter.Commented.HasValue)
            {
                var commentDict = isUser ? _userComments : _compComments;
                if ((commentDict.ContainsKey(policy.UniqueID) && !string.IsNullOrEmpty(commentDict[policy.UniqueID])) != _currentFilter.Commented.Value)
                    return false;
            }

            if (_currentFilter.AllowedProducts is not null && !PolicyProcessing.IsPolicySupported(policy, _currentFilter.AllowedProducts, _currentFilter.AlwaysMatchAny, _currentFilter.MatchBlankSupport))
                return false;
            return true;
        }

        public bool PolicyVisibleInSection(PolicyPlusPolicy policy, AdmxPolicySection section) => (policy.RawPolicy.Section & section) > 0;

        public PolFile GetOrCreatePolFromPolicySource(IPolicySource source)
        {
            if (source is PolFile file)
                return file;
            if (source is RegistryPolicyProxy proxy)
            {
                var regRoot = proxy.EncapsulatedRegistry;
                var pol = new PolFile();
                void addSubtree(string pathRoot, RegistryKey key)
                {
                    foreach (var valName in key.GetValueNames())
                    {
                        var valData = key.GetValue(valName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                        if (valData is int i)
                            valData = new ReinterpretableDword { Signed = i }.Unsigned;
                        else if (valData is long l)
                            valData = new ReinterpretableQword { Signed = l }.Unsigned;
                        pol.SetValue(pathRoot, valName, valData, key.GetValueKind(valName));
                    }

                    foreach (var subkeyName in key.GetSubKeyNames())
                    {
                        using var subkey = key.OpenSubKey(subkeyName, false);
                        addSubtree(pathRoot + @"\" + subkeyName, subkey);
                    }
                }

                foreach (var policyPath in RegistryPolicyProxy.PolicyKeys)
                {
                    using var policyKey = regRoot.OpenSubKey(policyPath, false);
                    addSubtree(policyPath, policyKey);
                }

                return pol;
            }

            throw new InvalidOperationException("Policy source type not supported");
        }

        public MsgBoxResult DisplayAdmxLoadErrorReport(IEnumerable<AdmxLoadFailure> failures, bool askContinue = false)
        {
            if (!failures.Any())
                return MsgBoxResult.OK;
            var boxButtons = askContinue ? MsgBoxButtons.YesNo : MsgBoxButtons.OK;
            string header = "Errors were encountered while adding administrative templates to the workspace.";
            return MsgBoxCompat.Show(header + (askContinue ? " Continue trying to use this workspace?" : "") + Environment.NewLine + Environment.NewLine + string.Join(Environment.NewLine + Environment.NewLine, failures.Select(f => f.ToString())), boxButtons, MsgBoxIcon.Warning);
        }

        public string GetPreferredLanguageCode() => Convert.ToString(_configuration.GetValue("LanguageCode", System.Globalization.CultureInfo.CurrentCulture.Name));

        public static string PrettifyDescription(string description)
        {
            var sb = new StringBuilder();
            foreach (var line in description.Split(Environment.NewLine))
                sb.AppendLine(line.Trim());
            return sb.ToString().TrimEnd();
        }

        // ------------------------------------------------------------------
        // Categories tree selection
        // ------------------------------------------------------------------

        private void SelectTreeNode(CategoryNodeViewModel node)
        {
            if (ReferenceEquals(_selectedTreeNode, node))
                return;
            if (_selectedTreeNode is not null)
                _selectedTreeNode.IsSelected = false;
            _selectedTreeNode = node;
            if (node is not null)
                node.IsSelected = true;
        }

        private void CategoriesTree_SelectedItemChanged(object sender, RoutedPropertyChangedEventArgs<object> e)
        {
            _selectedTreeNode = e.NewValue as CategoryNodeViewModel;
            _currentCategory = _selectedTreeNode?.Category;
            _favoritesSelected = _selectedTreeNode is not null && _selectedTreeNode.Category is null;
            UpdateCategoryListing();
            ClearSelections();
            UpdatePolicyInfo();
        }

        private static T FindAncestor<T>(DependencyObject current) where T : DependencyObject
        {
            while (current is not null)
            {
                if (current is T match)
                    return match;
                current = VisualTreeHelper.GetParent(current);
            }

            return null;
        }

        private void CategoriesTree_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            // Right-clicking doesn't select the node by default, unlike a left click.
            if (FindAncestor<System.Windows.Controls.TreeViewItem>(e.OriginalSource as DependencyObject)?.DataContext is not CategoryNodeViewModel node)
                return;
            SelectTreeNode(node);
            ShowPolicyObjectContext(CategoriesTree, node.Category, showingForCategory: true);
        }

        private void PoliciesList_PreviewMouseRightButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (FindAncestor<System.Windows.Controls.ListViewItem>(e.OriginalSource as DependencyObject)?.DataContext is not PolicyRowViewModel row)
                return;
            PoliciesList.SelectedItem = row;
            ShowPolicyObjectContext(PoliciesList, row.Tag, row.Tag is PolicyPlusCategory);
        }

        // ------------------------------------------------------------------
        // Right-click context menu (shared by CategoriesTree and PoliciesList - see
        // PolicyObjectContext's remarks in the XAML for why it's reparented per use)
        // ------------------------------------------------------------------

        private object _contextTarget;

        private void ShowPolicyObjectContext(FrameworkElement target, object polObject, bool showingForCategory)
        {
            _contextTarget = polObject;
            foreach (var item in PolicyObjectContext.Items.OfType<System.Windows.Controls.MenuItem>())
            {
                string tag = item.Tag as string;
                bool ok = true;
                if (tag == "P" && showingForCategory)
                    ok = false;
                if (tag == "C" && !showingForCategory)
                    ok = false;
                item.Visibility = ok ? Visibility.Visible : Visibility.Collapsed;
            }

            if (!showingForCategory && polObject is PolicyPlusPolicy policy)
                CmeFavoriteToggle.Header = _favoriteIds.Contains(policy.UniqueID) ? "Remove from Favorites" : "Add to Favorites";
            CmeCopySeparator.Visibility = showingForCategory ? Visibility.Collapsed : Visibility.Visible;

            CategoriesTree.ContextMenu = null;
            PoliciesList.ContextMenu = null;
            PolicyObjectContext.PlacementTarget = target;
            target.ContextMenu = PolicyObjectContext;
        }

        private void CmeCatOpen_Click(object sender, RoutedEventArgs e)
        {
            _currentCategory = (PolicyPlusCategory)_contextTarget;
            UpdateCategoryListing();
        }

        private void CmePolEdit_Click(object sender, RoutedEventArgs e) => ShowSettingEditor((PolicyPlusPolicy)_contextTarget, _viewPolicyTypes);

        private void CmeFavoriteToggle_Click(object sender, RoutedEventArgs e)
        {
            string id = ((PolicyPlusPolicy)_contextTarget).UniqueID;
            if (!_favoriteIds.Remove(id))
                _favoriteIds.Add(id);
            _configuration.SetValue("Favorites", _favoriteIds.ToArray());
            if (_favoritesSelected)
                UpdateCategoryListing();
        }

        private void CmeAllDetails_Click(object sender, RoutedEventArgs e)
        {
            if (_contextTarget is PolicyPlusCategory category)
                DetailCategoryWindow.PresentDialog(this, category);
            else
                DetailPolicyWindow.PresentDialog(this, (PolicyPlusPolicy)_contextTarget);
        }

        private void CmePolInspectElements_Click(object sender, RoutedEventArgs e) =>
            InspectPolicyElementsWindow.PresentDialog(this, (PolicyPlusPolicy)_contextTarget, _icons, _admxWorkspace);

        private void CmePolSpolFragment_Click(object sender, RoutedEventArgs e) =>
            InspectSpolFragmentWindow.PresentDialog(this, (PolicyPlusPolicy)_contextTarget, _admxWorkspace, _compPolicySource, _userPolicySource, _compComments, _userComments);

        private void CmeCopyId_Click(object sender, RoutedEventArgs e) => Clipboard.SetText(((PolicyPlusPolicy)_contextTarget).UniqueID);

        private void CmeCopyName_Click(object sender, RoutedEventArgs e) => Clipboard.SetText(((PolicyPlusPolicy)_contextTarget).DisplayName);

        private void CmeCopyRegPath_Click(object sender, RoutedEventArgs e)
        {
            var rawPolicy = ((PolicyPlusPolicy)_contextTarget).RawPolicy;
            string root = rawPolicy.Section == AdmxPolicySection.User ? @"HKEY_CURRENT_USER\" : @"HKEY_LOCAL_MACHINE\";
            string path = root + rawPolicy.RegistryKey;
            if (!string.IsNullOrEmpty(rawPolicy.RegistryValue))
                path += @"\" + rawPolicy.RegistryValue;
            Clipboard.SetText(path);
        }

        // ------------------------------------------------------------------
        // Policies list
        // ------------------------------------------------------------------

        private void PoliciesList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (PoliciesList.SelectedItem is PolicyRowViewModel row)
            {
                if (row.Tag is PolicyPlusPolicy policy)
                {
                    _currentSetting = policy;
                    _highlightCategory = null;
                }
                else if (row.Tag is PolicyPlusCategory category)
                {
                    _highlightCategory = category;
                    _currentSetting = null;
                }
            }
            else
            {
                ClearSelections();
            }

            UpdatePolicyInfo();
        }

        private void PoliciesList_MouseDoubleClick(object sender, MouseButtonEventArgs e)
        {
            if (PoliciesList.SelectedItem is not PolicyRowViewModel row)
                return;
            if (row.Tag is PolicyPlusCategory category)
            {
                _currentCategory = category;
                UpdateCategoryListing();
            }
            else if (row.Tag is PolicyPlusPolicy policy)
            {
                ShowSettingEditor(policy, _viewPolicyTypes);
            }
        }

        private void PoliciesList_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter && PoliciesList.SelectedItem is not null)
                PoliciesList_MouseDoubleClick(sender, null);
        }

        private void PoliciesList_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            // Fit the policy name column to the window size, but capped - letting it fill 100% of
            // whatever's left wastes huge amounts of space on wide windows.
            double fixedWidth = StateColumn.Width + CommentColumn.Width + IdColumn.Width;
            double available = PoliciesList.ActualWidth - fixedWidth - 30;
            NameColumn.Width = Math.Max(300, Math.Min(available, 460));
        }

        // ------------------------------------------------------------------
        // View / search
        // ------------------------------------------------------------------

        private void ComboAppliesTo_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_configuration is null)
                return; // Fires once from the XAML-declared SelectedIndex="0" before Window_Loaded runs
            string text = (ComboAppliesTo.SelectedItem as ComboBoxItem)?.Content as string;
            _viewPolicyTypes = text switch
            {
                "User" => AdmxPolicySection.User,
                "Computer" => AdmxPolicySection.Machine,
                _ => AdmxPolicySection.Both,
            };
            MoveToVisibleCategoryAndReload();
        }

        private void SearchTextbox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                e.Handled = true;
                RunSearch();
            }
            else if (e.Key == Key.Escape)
            {
                ClearSearch();
            }
        }

        private void SearchButton_Click(object sender, RoutedEventArgs e) => RunSearch();

        private void ClearSearchButton_Click(object sender, RoutedEventArgs e) => ClearSearch();

        private void ClearSearch()
        {
            SearchTextbox.Text = "";
            RunSearch();
        }

        private void RunSearch()
        {
            string query = SearchTextbox.Text;
            _searchMatcher = string.IsNullOrWhiteSpace(query) ? null : PolicySearch.BuildMatcher(PolicySearch.ToSubstringQuery(query), true, true, true, true, true, _compComments, _userComments);
            MoveToVisibleCategoryAndReload();
        }

        private void EmptyCategoriesMenuItem_Click(object sender, RoutedEventArgs e)
        {
            _viewEmptyCategories = EmptyCategoriesMenuItem.IsChecked;
            MoveToVisibleCategoryAndReload();
        }

        private void OnlyFilteredObjectsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            _viewFilteredOnly = OnlyFilteredObjectsMenuItem.IsChecked;
            MoveToVisibleCategoryAndReload();
        }

        private void FilterOptionsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var newFilter = FilterOptionsWindow.PresentDialog(this, _currentFilter, _admxWorkspace);
            if (newFilter is not null)
            {
                _currentFilter = newFilter;
                _viewFilteredOnly = true;
                OnlyFilteredObjectsMenuItem.IsChecked = true;
                MoveToVisibleCategoryAndReload();
            }
        }

        private void DeduplicatePoliciesMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ClearSelections();
            int deduped = PolicyProcessing.DeduplicatePolicies(_admxWorkspace);
            if (deduped > 0)
                _isDirty = true;
            MsgBoxCompat.Show("Deduplicated " + deduped + " policies.", MsgBoxButtons.OK, MsgBoxIcon.Information);
            UpdateCategoryListing();
            UpdatePolicyInfo();
        }

        private void LoadedAdmxFilesMenuItem_Click(object sender, RoutedEventArgs e) => LoadedAdmxWindow.PresentDialog(this, _admxWorkspace);

        private void AllProductsMenuItem_Click(object sender, RoutedEventArgs e) => LoadedProductsWindow.PresentDialog(this, _admxWorkspace);

        private void AllSupportDefinitionsMenuItem_Click(object sender, RoutedEventArgs e) => LoadedSupportDefinitionsWindow.PresentDialog(this, _admxWorkspace);

        // ------------------------------------------------------------------
        // File menu
        // ------------------------------------------------------------------

        private void OpenAdmxFolderMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var openAdmxResult = OpenAdmxFolderWindow.PresentDialog(this);
            if (openAdmxResult is not null)
            {
                try
                {
                    if (openAdmxResult.Value.ClearWorkspace)
                        ClearAdmxWorkspace();
                    DisplayAdmxLoadErrorReport(_admxWorkspace.LoadFolder(openAdmxResult.Value.Folder, GetPreferredLanguageCode()));
                    if (openAdmxResult.Value.ClearWorkspace)
                        _configuration.SetValue("AdmxSource", openAdmxResult.Value.Folder);
                }
                catch (Exception ex)
                {
                    MsgBoxCompat.Show("The folder could not be fully added to the workspace. " + ex.Message, MsgBoxButtons.OK, MsgBoxIcon.Warning);
                }

                PopulateAdmxUi();
            }
        }

        private void OpenAdmxFileMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new Microsoft.Win32.OpenFileDialog { Filter = "Policy definitions files|*.admx", Title = "Open ADMX file" };
            if (ofd.ShowDialog() != true)
                return;
            try
            {
                DisplayAdmxLoadErrorReport(_admxWorkspace.LoadFile(ofd.FileName, GetPreferredLanguageCode()));
            }
            catch (Exception ex)
            {
                MsgBoxCompat.Show("The ADMX file could not be added to the workspace. " + ex.Message, MsgBoxButtons.OK, MsgBoxIcon.Warning);
            }

            PopulateAdmxUi();
        }

        private void CloseAdmxWorkspaceMenuItem_Click(object sender, RoutedEventArgs e)
        {
            ClearAdmxWorkspace();
            PopulateAdmxUi();
        }

        private void SetAdmlLanguageMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var newLanguage = LanguageOptionsWindow.PresentDialog(this, GetPreferredLanguageCode());
            if (newLanguage is not null)
            {
                _configuration.SetValue("LanguageCode", newLanguage);
                if (MsgBoxCompat.Show("Language changes will take effect when ADML files are next loaded. Would you like to reload the workspace now?", MsgBoxButtons.YesNo, MsgBoxIcon.Question) == MsgBoxResult.Yes)
                {
                    ClearAdmxWorkspace();
                    OpenLastAdmxSource();
                    PopulateAdmxUi();
                }
            }
        }

        private void OpenPolicyResourcesMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var openPolResult = OpenPolWindow.PresentDialog(this, _compPolicyLoader.Source, _compPolicyLoader.LoaderData, _userPolicyLoader.Source, _userPolicyLoader.LoaderData);
            if (openPolResult is not null)
            {
                OpenPolicyLoaders(openPolResult.Value.User, openPolResult.Value.Computer, false);
                MoveToVisibleCategoryAndReload();
            }
        }

        private void OpenRegFileMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new Microsoft.Win32.OpenFileDialog { Filter = "Registry scripts|*.reg" };
            if (ofd.ShowDialog() != true)
                return;
            RegFile reg;
            try
            {
                reg = RegFile.Load(ofd.FileName, "");
            }
            catch (Exception)
            {
                MsgBoxCompat.Show("Failed to load the REG file.", MsgBoxButtons.OK, MsgBoxIcon.Warning);
                return;
            }

            var hiveCounts = reg.CountKeysByHive();
            bool hasComputer = hiveCounts[RegFileHive.Computer] > 0;
            bool hasUser = hiveCounts[RegFileHive.User] > 0;
            if (!hasComputer && !hasUser)
            {
                MsgBoxCompat.Show("This REG file doesn't contain any Computer (HKEY_LOCAL_MACHINE) or User (HKEY_CURRENT_USER/HKEY_USERS) entries to import.", MsgBoxButtons.OK, MsgBoxIcon.Warning);
                return;
            }

            RegFileHive chosenHive;
            if (hasComputer && hasUser)
            {
                string msg = "This REG file mixes Computer and User entries (" + hiveCounts[RegFileHive.Computer] + " Computer key(s), " + hiveCounts[RegFileHive.User] + " User key(s)). Only one can be opened at a time this way." + Environment.NewLine + Environment.NewLine + "Click Yes to import the Computer entries (discarding the User entries), or No to import the User entries (discarding the Computer entries).";
                var result = MsgBoxCompat.Show(msg, MsgBoxButtons.YesNoCancel, MsgBoxIcon.Warning);
                if (result == MsgBoxResult.Cancel)
                    return;
                chosenHive = result == MsgBoxResult.Yes ? RegFileHive.Computer : RegFileHive.User;
            }
            else
            {
                chosenHive = hasComputer ? RegFileHive.Computer : RegFileHive.User;
            }

            var pol = new PolFile();
            reg.ApplyHive(pol, chosenHive);
            bool isUser = chosenHive == RegFileHive.User;
            var newLoader = new PolicyLoader(PolicyLoaderSource.Null, "", isUser);
            if (isUser)
            {
                _userPolicyLoader?.Close();
                _userPolicyLoader = newLoader;
                _userPolicySource = pol;
                _userComments = new Dictionary<string, string>();
                UserSourceLabel.Text = _userPolicyLoader.GetDisplayInfo();
            }
            else
            {
                _compPolicyLoader?.Close();
                _compPolicyLoader = newLoader;
                _compPolicySource = pol;
                _compComments = new Dictionary<string, string>();
                ComputerSourceLabel.Text = _compPolicyLoader.GetDisplayInfo();
            }

            _isDirty = true;
            ClearSelections();
            MoveToVisibleCategoryAndReload();
            string successMsg = "REG file opened as a standalone editable " + (isUser ? "User" : "Computer") + " source. Use Export POL/REG to save it - the normal Save Policies action discards scratch sources like this one.";
            if (reg.HasDefaultValues())
                successMsg = "This REG file set one or more keys' default values, which Group Policy has no way to represent - those specific entries were skipped." + Environment.NewLine + Environment.NewLine + successMsg;
            MsgBoxCompat.Show(successMsg, MsgBoxButtons.OK, MsgBoxIcon.Information);
        }

        private void SavePoliciesMenuItem_Click(object sender, RoutedEventArgs e)
        {
            void saveComments(Dictionary<string, string> comments, PolicyLoader loader)
            {
                try
                {
                    if (comments is not null)
                        CmtxFile.FromCommentTable(comments).Save(loader.GetCmtxPath());
                }
                catch (Exception)
                {
                }
            }

            saveComments(_userComments, _userPolicyLoader);
            saveComments(_compComments, _compPolicyLoader);
            try
            {
                string compStatus = "not writable";
                string userStatus = "not writable";
                if (_compPolicyLoader.GetWritability() == PolicySourceWritability.Writable)
                    compStatus = _compPolicyLoader.Save();
                if (_userPolicyLoader.GetWritability() == PolicySourceWritability.Writable)
                    userStatus = _userPolicyLoader.Save();
                _configuration.SetValue("CompSourceType", (int)_compPolicyLoader.Source);
                _configuration.SetValue("UserSourceType", (int)_userPolicyLoader.Source);
                _configuration.SetValue("CompSourceData", _compPolicyLoader.LoaderData ?? "");
                _configuration.SetValue("UserSourceData", _userPolicyLoader.LoaderData ?? "");
                _isDirty = false;
                MsgBoxCompat.Show("Success." + Environment.NewLine + Environment.NewLine + "User policies: " + userStatus + "." + Environment.NewLine + Environment.NewLine + "Computer policies: " + compStatus + ".", MsgBoxButtons.OK, MsgBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MsgBoxCompat.Show("Saving failed!" + Environment.NewLine + Environment.NewLine + ex.Message, MsgBoxButtons.OK, MsgBoxIcon.Warning);
            }
        }

        private void ResetAllToDefaultMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (MsgBoxCompat.Show("This will reset every configured policy back to Not Configured, across both Computer and User policies. This cannot be undone. Continue?", MsgBoxButtons.YesNo, MsgBoxIcon.Warning) != MsgBoxResult.Yes)
                return;
            ClearSelections();
            int reset = 0;
            foreach (var policy in _admxWorkspace.Policies.Values)
            {
                var section = policy.RawPolicy.Section;
                if (section == AdmxPolicySection.Both || section == AdmxPolicySection.Machine)
                {
                    if (PolicyProcessing.GetPolicyState(_compPolicySource, policy) != PolicyState.NotConfigured)
                    {
                        PolicyProcessing.ForgetPolicy(_compPolicySource, policy);
                        reset++;
                    }
                }

                if (section == AdmxPolicySection.Both || section == AdmxPolicySection.User)
                {
                    if (PolicyProcessing.GetPolicyState(_userPolicySource, policy) != PolicyState.NotConfigured)
                    {
                        PolicyProcessing.ForgetPolicy(_userPolicySource, policy);
                        reset++;
                    }
                }
            }

            if (reset > 0)
                _isDirty = true;
            MsgBoxCompat.Show("Reset " + reset + " policy configuration" + (reset == 1 ? "" : "s") + " to Not Configured.", MsgBoxButtons.OK, MsgBoxIcon.Information);
            UpdateCategoryListing();
            UpdatePolicyInfo();
        }

        private void EditRawPolMenuItem_Click(object sender, RoutedEventArgs e)
        {
            bool userIsPol = _userPolicySource is PolFile;
            bool compIsPol = _compPolicySource is PolFile;
            if (!(userIsPol || compIsPol))
            {
                MsgBoxCompat.Show("Neither loaded source is backed by a POL file.", MsgBoxButtons.OK, MsgBoxIcon.Warning);
                return;
            }

            if (Convert.ToInt32(_configuration.GetValue("EditPolDangerAcknowledged", 0)) == 0)
            {
                if (MsgBoxCompat.Show("Caution! This tool is for very advanced users. Improper modifications may result in inconsistencies in policies' states." + Environment.NewLine + Environment.NewLine + "Changes operate directly on the policy source, though they will not be committed to disk until you save. Are you sure you want to continue?", MsgBoxButtons.YesNo, MsgBoxIcon.Warning) == MsgBoxResult.No)
                    return;
                _configuration.SetValue("EditPolDangerAcknowledged", 1);
            }

            var editPolSection = OpenSectionWindow.PresentDialog(this, userIsPol, compIsPol);
            if (editPolSection is not null)
            {
                EditPolWindow.PresentDialog(this, _icons, (PolFile)(editPolSection == AdmxPolicySection.Machine ? _compPolicySource : _userPolicySource), editPolSection == AdmxPolicySection.User);
                _isDirty = true;
            }

            MoveToVisibleCategoryAndReload();
        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e) => Close();

        // ------------------------------------------------------------------
        // Find menu
        // ------------------------------------------------------------------

        private void ByIdMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var findByIdResult = FindByIdWindow.PresentDialog(this, _admxWorkspace, _icons);
            if (findByIdResult is not null)
            {
                var selCat = findByIdResult.SelectedCategory;
                var selPol = findByIdResult.SelectedPolicy;
                var selPro = findByIdResult.SelectedProduct;
                var selSup = findByIdResult.SelectedSupport;
                if (selCat is not null)
                {
                    if (_categoryNodes.ContainsKey(selCat))
                    {
                        _currentCategory = selCat;
                        UpdateCategoryListing();
                    }
                    else
                    {
                        MsgBoxCompat.Show("The category is not currently visible. Change the view settings and try again.", MsgBoxButtons.OK, MsgBoxIcon.Warning);
                    }
                }
                else if (selPol is not null)
                {
                    ShowSettingEditor(selPol, (AdmxPolicySection)Math.Min((int)_viewPolicyTypes, (int)findByIdResult.SelectedSection));
                    FocusPolicy(selPol);
                }
                else if (selPro is not null)
                {
                    DetailProductWindow.PresentDialog(this, selPro);
                }
                else if (selSup is not null)
                {
                    DetailSupportWindow.PresentDialog(this, selSup);
                }
                else
                {
                    MsgBoxCompat.Show("That object could not be found.", MsgBoxButtons.OK, MsgBoxIcon.Warning);
                }
            }
        }

        private void ByTextMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var textSearcher = FindByTextWindow.PresentDialog(this, _userComments, _compComments);
            if (textSearcher is not null)
                ShowSearchDialog(textSearcher);
        }

        private void SearchResultsMenuItem_Click(object sender, RoutedEventArgs e) => ShowSearchDialog(null);

        private void FindNextMenuItem_Click(object sender, RoutedEventArgs e)
        {
            while (true)
            {
                var nextPol = FindResultsWindow.NextPolicy();
                if (nextPol is null)
                {
                    MsgBoxCompat.Show("There are no more results that match the filter.", MsgBoxButtons.OK, MsgBoxIcon.Information);
                    break;
                }

                if (ShouldShowPolicy(nextPol))
                {
                    FocusPolicy(nextPol);
                    break;
                }
            }
        }

        private void ByRegistryMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var registrySearcher = FindByRegistryWindow.PresentDialog(this);
            if (registrySearcher is not null)
                ShowSearchDialog(registrySearcher);
        }

        // ------------------------------------------------------------------
        // Share menu
        // ------------------------------------------------------------------

        private void ImportSemanticPolicyMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var spol = ImportSpolWindow.PresentDialog(this);
            if (spol is not null)
            {
                int fails = spol.ApplyAll(_admxWorkspace, _userPolicySource, _compPolicySource, _userComments, _compComments);
                _isDirty = true;
                MoveToVisibleCategoryAndReload();
                if (fails == 0)
                    MsgBoxCompat.Show("Semantic Policy successfully applied.", MsgBoxButtons.OK, MsgBoxIcon.Information);
                else
                    MsgBoxCompat.Show(fails + " out of " + spol.Policies.Count + " could not be applied.", MsgBoxButtons.OK, MsgBoxIcon.Warning);
            }
        }

        private void ImportPolMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var ofd = new Microsoft.Win32.OpenFileDialog { Filter = "POL files|*.pol" };
            if (ofd.ShowDialog() != true)
                return;
            PolFile pol;
            try
            {
                pol = PolFile.Load(ofd.FileName);
            }
            catch (Exception)
            {
                MsgBoxCompat.Show("The POL file could not be loaded.", MsgBoxButtons.OK, MsgBoxIcon.Warning);
                return;
            }

            var importSection = OpenSectionWindow.PresentDialog(this, true, true);
            if (importSection is not null)
            {
                var section = importSection == AdmxPolicySection.User ? _userPolicySource : _compPolicySource;
                pol.Apply(section);
                _isDirty = true;
                MoveToVisibleCategoryAndReload();
                MsgBoxCompat.Show("POL import successful.", MsgBoxButtons.OK, MsgBoxIcon.Information);
            }
        }

        private void ImportRegMenuItem_Click(object sender, RoutedEventArgs e)
        {
            // ImportRegWindow infers Computer vs User from the REG file's own hive path once a
            // file is chosen, only falling back to asking if the file genuinely mixes both hives.
            if (ImportRegWindow.PresentDialog(this, _userPolicySource, _compPolicySource))
            {
                _isDirty = true;
                MoveToVisibleCategoryAndReload();
            }
        }

        private void ExportPolMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var sfd = new Microsoft.Win32.SaveFileDialog { Filter = "POL files|*.pol" };
            AdmxPolicySection? exportSection = sfd.ShowDialog() == true ? OpenSectionWindow.PresentDialog(this, true, true) : null;
            if (exportSection is not null)
            {
                var section = exportSection == AdmxPolicySection.Machine ? _compPolicySource : _userPolicySource;
                try
                {
                    GetOrCreatePolFromPolicySource(section).Save(sfd.FileName);
                    MsgBoxCompat.Show("POL exported successfully.", MsgBoxButtons.OK, MsgBoxIcon.Information);
                }
                catch (Exception)
                {
                    MsgBoxCompat.Show("The POL file could not be saved.", MsgBoxButtons.OK, MsgBoxIcon.Warning);
                }
            }
        }

        private void ExportRegMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var exportRegSection = OpenSectionWindow.PresentDialog(this, true, true);
            if (exportRegSection is not null)
            {
                var source = exportRegSection == AdmxPolicySection.Machine ? _compPolicySource : _userPolicySource;
                ExportRegWindow.PresentDialog(this, "", GetOrCreatePolFromPolicySource(source), exportRegSection == AdmxPolicySection.User);
            }
        }

        // ------------------------------------------------------------------
        // Options menu (color mode - applies live, no restart needed)
        // ------------------------------------------------------------------

        private void SetColorModeMenuChecks(string colorMode)
        {
            LightMenuItem.IsChecked = colorMode == "Light";
            DarkMenuItem.IsChecked = colorMode == "Dark";
            SystemMenuItem.IsChecked = colorMode == "System";
        }

        private void ApplyColorModeChoice(string colorMode)
        {
            ThemeService.Persist(colorMode);
            SetColorModeMenuChecks(colorMode);
        }

        private void LightMenuItem_Click(object sender, RoutedEventArgs e) => ApplyColorModeChoice("Light");

        private void DarkMenuItem_Click(object sender, RoutedEventArgs e) => ApplyColorModeChoice("Dark");

        private void SystemMenuItem_Click(object sender, RoutedEventArgs e) => ApplyColorModeChoice("System");

        // ------------------------------------------------------------------
        // Help menu
        // ------------------------------------------------------------------

        private void AboutMenuItem_Click(object sender, RoutedEventArgs e) => AboutWindow.PresentDialog(this);

        private void AcquireAdmxFilesMenuItem_Click(object sender, RoutedEventArgs e)
        {
            var newAdmxFolder = DownloadAdmxWindow.PresentDialog(this);
            if (!string.IsNullOrEmpty(newAdmxFolder))
            {
                ClearAdmxWorkspace();
                DisplayAdmxLoadErrorReport(_admxWorkspace.LoadFolder(newAdmxFolder, GetPreferredLanguageCode()));
                _configuration.SetValue("AdmxSource", newAdmxFolder);
                PopulateAdmxUi();
            }
        }

        // ------------------------------------------------------------------
        // Keyboard shortcuts (mirrors the original ToolStripMenuItem.ShortcutKeys)
        // ------------------------------------------------------------------

        private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            bool ctrl = Keyboard.Modifiers.HasFlag(ModifierKeys.Control);
            bool shift = Keyboard.Modifiers.HasFlag(ModifierKeys.Shift);
            if (ctrl && e.Key == Key.O)
            {
                OpenPolicyResourcesMenuItem_Click(sender, null);
                e.Handled = true;
            }
            else if (ctrl && e.Key == Key.S)
            {
                SavePoliciesMenuItem_Click(sender, null);
                e.Handled = true;
            }
            else if (ctrl && e.Key == Key.G)
            {
                ByIdMenuItem_Click(sender, null);
                e.Handled = true;
            }
            else if (ctrl && e.Key == Key.F)
            {
                ByTextMenuItem_Click(sender, null);
                e.Handled = true;
            }
            else if (ctrl && e.Key == Key.R)
            {
                ByRegistryMenuItem_Click(sender, null);
                e.Handled = true;
            }
            else if (!ctrl && shift && e.Key == Key.F3)
            {
                SearchResultsMenuItem_Click(sender, null);
                e.Handled = true;
            }
            else if (!ctrl && !shift && e.Key == Key.F3)
            {
                FindNextMenuItem_Click(sender, null);
                e.Handled = true;
            }
        }

        // ------------------------------------------------------------------
        // Window lifecycle / persisted layout
        // ------------------------------------------------------------------

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            SaveWindowBounds();
            SavePaneLayout();
            if (!_isDirty)
                return;
            var result = MsgBoxCompat.Show("There are unsaved changes. Save before closing?", MsgBoxButtons.YesNoCancel, MsgBoxIcon.Question);
            if (result == MsgBoxResult.Cancel)
            {
                e.Cancel = true;
                return;
            }

            if (result == MsgBoxResult.Yes)
            {
                SavePoliciesMenuItem_Click(sender, null);
                if (_isDirty)
                    e.Cancel = true;
            }
        }

        private void Window_Closed(object sender, EventArgs e) => ClosePolicySources();

        private void RestoreWindowBounds()
        {
            int left = Convert.ToInt32(_configuration.GetValue("WindowLeft", int.MinValue));
            int top = Convert.ToInt32(_configuration.GetValue("WindowTop", int.MinValue));
            int width = Convert.ToInt32(_configuration.GetValue("WindowWidth", 0));
            int height = Convert.ToInt32(_configuration.GetValue("WindowHeight", 0));
            if (left == int.MinValue || top == int.MinValue || width <= 0 || height <= 0)
                return;
            var bounds = new Rect(left, top, width, height);
            // Guard against a saved position from a monitor/resolution that's since changed -
            // WPF has no per-monitor enumeration without P/Invoke, so this checks against the
            // combined bounding box of all monitors instead of each one individually.
            var virtualScreen = new Rect(SystemParameters.VirtualScreenLeft, SystemParameters.VirtualScreenTop, SystemParameters.VirtualScreenWidth, SystemParameters.VirtualScreenHeight);
            if (!virtualScreen.IntersectsWith(bounds))
                return;
            Left = left;
            Top = top;
            Width = width;
            Height = height;
            if (Convert.ToInt32(_configuration.GetValue("WindowMaximized", 0)) == 1)
                WindowState = WindowState.Maximized;
        }

        private void SaveWindowBounds()
        {
            if (WindowState == WindowState.Minimized)
                return; // Not a useful geometry to restore to; keep the last saved Normal/Maximized bounds
            bool maximized = WindowState == WindowState.Maximized;
            var bounds = maximized ? RestoreBounds : new Rect(Left, Top, Width, Height);
            _configuration.SetValue("WindowLeft", (int)bounds.Left);
            _configuration.SetValue("WindowTop", (int)bounds.Top);
            _configuration.SetValue("WindowWidth", (int)bounds.Width);
            _configuration.SetValue("WindowHeight", (int)bounds.Height);
            _configuration.SetValue("WindowMaximized", maximized ? 1 : 0);
        }

        private void RestorePaneLayout()
        {
            int treeWidth = Convert.ToInt32(_configuration.GetValue("TreePaneWidth", 0));
            if (treeWidth > 0)
                TreeColumn.Width = new GridLength(treeWidth);
            int descWidth = Convert.ToInt32(_configuration.GetValue("DescriptionPaneWidth", 0));
            if (descWidth > 0)
                DescriptionColumn.Width = new GridLength(descWidth);
            int stateWidth = Convert.ToInt32(_configuration.GetValue("ColumnWidthState", 0));
            if (stateWidth > 0)
                StateColumn.Width = stateWidth;
            int commentWidth = Convert.ToInt32(_configuration.GetValue("ColumnWidthComment", 0));
            if (commentWidth > 0)
                CommentColumn.Width = commentWidth;
            int idWidth = Convert.ToInt32(_configuration.GetValue("ColumnWidthId", 0));
            if (idWidth > 0)
                IdColumn.Width = idWidth;
        }

        private void SavePaneLayout()
        {
            _configuration.SetValue("TreePaneWidth", (int)TreeColumn.ActualWidth);
            _configuration.SetValue("DescriptionPaneWidth", (int)DescriptionColumn.ActualWidth);
            _configuration.SetValue("ColumnWidthState", (int)StateColumn.Width);
            _configuration.SetValue("ColumnWidthComment", (int)CommentColumn.Width);
            _configuration.SetValue("ColumnWidthId", (int)IdColumn.Width);
        }
    }
}
