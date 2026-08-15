using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using Microsoft.VisualBasic;
using Microsoft.VisualBasic.CompilerServices;
using Microsoft.Win32;

namespace PolicyPlus
{
    public partial class Main
    {
        private ConfigurationStorage Configuration;
        private AdmxBundle AdmxWorkspace = new AdmxBundle();
        private IPolicySource UserPolicySource, CompPolicySource;
        private PolicyLoader UserPolicyLoader, CompPolicyLoader;
        private Dictionary<string, string> UserComments, CompComments;
        private PolicyPlusCategory CurrentCategory;
        private PolicyPlusPolicy CurrentSetting;
        private FilterConfiguration CurrentFilter = new FilterConfiguration();
        private PolicyPlusCategory HighlightCategory;
        private Dictionary<PolicyPlusCategory, TreeNode> CategoryNodes = new Dictionary<PolicyPlusCategory, TreeNode>();
        private bool ViewEmptyCategories = false;
        private AdmxPolicySection ViewPolicyTypes = AdmxPolicySection.Both;
        private bool ViewFilteredOnly = false;
        private bool _isDirty = false;
        private bool _pendingRestartForColorMode = false;
        private List<string> FavoriteIds = new List<string>();
        private TreeNode FavoritesNode;
        private Func<PolicyPlusPolicy, bool> SearchMatcher;
        private Color SelectionBackColor;
        private Color SelectionForeColor;
        private int _sortColumn = -1;
        private bool _sortAscending = true;

        public Main()
        {
            InitializeComponent();
            // CategoriesTree/PoliciesList became owner-drawn (see CategoriesTree_DrawNode/
            // PoliciesList_DrawSubItem) so the selection highlight can use an explicit color instead
            // of the theme's low-contrast one. Owner-drawn controls repaint every visible row through
            // managed GDI+ instead of the native control's own (already double-buffered) painting, so
            // without this they visibly flicker/redraw during a window resize - worse the longer the
            // list is, since every visible row repaints on every intermediate size. DoubleBuffered
            // isn't publicly settable on TreeView/ListView, hence the reflection.
            SetDoubleBuffered(CategoriesTree);
            SetDoubleBuffered(PoliciesList);
        }
        private static void SetDoubleBuffered(Control control)
        {
            typeof(Control).GetProperty("DoubleBuffered", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic)
                .SetValue(control, true, null);
        }
        private void Main_Load(object sender, EventArgs e)
        {
            // Enable the newest TLS versions supported by this Framework version (keeping Tls for Vista compatibility)
            System.Net.ServicePointManager.SecurityProtocol = System.Net.SecurityProtocolType.Tls | System.Net.SecurityProtocolType.Tls11 | System.Net.SecurityProtocolType.Tls12;
            // Create the configuration manager (for the Registry)
            Configuration = new ConfigurationStorage(RegistryHive.CurrentUser, @"Software\Policy Plus");
            Text = $"Policy Plus {VersionHolder.AppVersion}";
            RestoreWindowBounds();
            RestorePaneLayout();
            SetColorModeMenuChecks(Conversions.ToString(Configuration.GetValue("ColorMode", "System")));
            FavoriteIds = ((string[])Configuration.GetValue("Favorites", Array.Empty<string>())).ToList();
            if (Application.IsDarkModeEnabled)
            {
                // Plain Panel/Label controls aren't covered by the built-in dark renderer (unlike TreeView/
                // ListView/MenuStrip) - match the description pane and its container to whatever dark shade
                // the framework actually picked for CategoriesTree, rather than guessing a hardcoded color.
                // SplitContainer.Panel2.BackColor is explicitly hardcoded to Color.White in the Designer
                // (Main.Designer.cs), which otherwise shows through as a thin light strip between the two.
                SettingInfoPanel.BackColor = CategoriesTree.BackColor;
                SettingInfoPanel.ForeColor = CategoriesTree.ForeColor;
                SplitContainer.Panel2.BackColor = CategoriesTree.BackColor;
                SelectionBackColor = Color.FromArgb(0x2E, 0x75, 0xB6); // brighter dark-mode blue
                SelectionForeColor = Color.White;
            }
            else
            {
                SelectionBackColor = Color.FromArgb(0xA8, 0xD4, 0xF7); // brighter light-mode blue
                SelectionForeColor = Color.Black;
            }
            // Restore the last ADMX source and policy loaders
            OpenLastAdmxSource();
            PolicyLoaderSource compLoaderType = (PolicyLoaderSource)Conversions.ToInteger(Configuration.GetValue("CompSourceType", 0));
            var compLoaderData = Configuration.GetValue("CompSourceData", "");
            PolicyLoaderSource userLoaderType = (PolicyLoaderSource)Conversions.ToInteger(Configuration.GetValue("UserSourceType", 0));
            var userLoaderData = Configuration.GetValue("UserSourceData", "");
            try
            {
                OpenPolicyLoaders(new PolicyLoader(userLoaderType, Conversions.ToString(userLoaderData), true), new PolicyLoader(compLoaderType, Conversions.ToString(compLoaderData), false), true);
            }
            catch (Exception ex)
            {
                MsgBoxCompat.Show("The previous policy sources are not accessible. The defaults will be loaded.", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                Configuration.SetValue("CompSourceType", (int)PolicyLoaderSource.LocalGpo);
                Configuration.SetValue("UserSourceType", (int)PolicyLoaderSource.LocalGpo);
                OpenPolicyLoaders(new PolicyLoader(PolicyLoaderSource.LocalGpo, "", true), new PolicyLoader(PolicyLoaderSource.LocalGpo, "", false), true);
            }
            My.MyProject.Forms.OpenPol.SetLastSources(compLoaderType, Conversions.ToString(compLoaderData), userLoaderType, Conversions.ToString(userLoaderData));
            // Set up the UI
            ComboAppliesTo.Text = Conversions.ToString(ComboAppliesTo.Items[0]);
            CategoriesTree.Height -= InfoStrip.ClientSize.Height;
            SettingInfoPanel.Height -= InfoStrip.ClientSize.Height;
            PoliciesList.Height -= InfoStrip.ClientSize.Height;
            InfoStrip.Items.Insert(2, new ToolStripSeparator());
            PopulateAdmxUi();
        }
        private void Main_Shown(object sender, EventArgs e)
        {
            // Check whether ADMX files probably need to be downloaded
            if (Conversions.ToInteger(Configuration.GetValue("CheckedPolicyDefinitions", 0)) == 0)
            {
                Configuration.SetValue("CheckedPolicyDefinitions", 1);
                if (!SystemInfo.HasGroupPolicyInfrastructure() && AdmxWorkspace.Categories.Values.Where(c => IsOrphanCategory(c) & !IsEmptyCategory(c)).Count() > 2)
                {
                    if (MsgBoxCompat.Show($"Welcome to Policy Plus!{Constants.vbCrLf}{Constants.vbCrLf}Home editions do not come with the full set of policy definitions. Would you like to download them now? " + "This can also be done later with Help | Acquire ADMX Files.", MessageBoxButtons.YesNo, MessageBoxIcon.Information) == DialogResult.Yes)
                    {
                        AcquireADMXFilesToolStripMenuItem_Click(null, null);
                    }
                }
            }
        }
        public void OpenLastAdmxSource()
        {
            string defaultAdmxSource = Environment.ExpandEnvironmentVariables(@"%windir%\PolicyDefinitions");
            string admxSource = Conversions.ToString(Configuration.GetValue("AdmxSource", defaultAdmxSource));
            try
            {
                var fails = AdmxWorkspace.LoadFolder(admxSource, GetPreferredLanguageCode());
                if (DisplayAdmxLoadErrorReport(fails, true) == DialogResult.No)
                    throw new Exception("You decided to not use the problematic ADMX bundle.");
            }
            catch (Exception ex)
            {
                AdmxWorkspace = new AdmxBundle();
                string loadFailReason = "";
                if ((admxSource ?? "") != (defaultAdmxSource ?? ""))
                {
                    if (MsgBoxCompat.Show("Policy definitions could not be loaded from \"" + admxSource + "\": " + ex.Message + Constants.vbCrLf + Constants.vbCrLf + "Load from the default location?", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                    {
                        try
                        {
                            Configuration.SetValue("AdmxSource", defaultAdmxSource);
                            AdmxWorkspace = new AdmxBundle();
                            DisplayAdmxLoadErrorReport(AdmxWorkspace.LoadFolder(defaultAdmxSource, GetPreferredLanguageCode()));
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
                    MsgBoxCompat.Show("Policy definitions could not be loaded: " + loadFailReason, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }
        public void PopulateAdmxUi()
        {
            // Populate the left categories tree
            CategoriesTree.Nodes.Clear();
            CategoryNodes.Clear();
            var visibilityCache = new Dictionary<PolicyPlusCategory, bool>();
            void addCategory(IEnumerable<PolicyPlusCategory> CategoryList, TreeNodeCollection ParentNode) { foreach (var category in CategoryList.Where(c => ShouldShowCategoryCore(c, visibilityCache))) { var newNode = ParentNode.Add(category.UniqueID, category.DisplayName, GetImageIndexForCategory(category)); newNode.SelectedImageIndex = 3; newNode.Tag = category; CategoryNodes.Add(category, newNode); addCategory(category.Children, newNode.Nodes); } } // "Go" arrow
            addCategory(AdmxWorkspace.Categories.Values, CategoriesTree.Nodes);
            CategoriesTree.Sort();
            // Pin a synthetic Favorites node at the top - not a real category, so it's built
            // outside addCategory and inserted at index 0 after Sort() (which would otherwise
            // reorder it alphabetically like any other node)
            FavoritesNode = new TreeNode("★ Favorites");
            CategoriesTree.Nodes.Insert(0, FavoritesNode);
            CurrentCategory = null;
            UpdateCategoryListing();
            ClearSelections();
            UpdatePolicyInfo();
        }
        public void UpdateCategoryListing()
        {
            if (ReferenceEquals(CategoriesTree.SelectedNode, FavoritesNode))
            {
                UpdateFavoritesListing();
                return;
            }
            // Update the right pane to include the current category's children
            var topItemIndex = default(int?);
            if (PoliciesList.TopItem is not null)
                topItemIndex = PoliciesList.TopItem.Index;
            bool inSameCategory = false;
            PoliciesList.Items.Clear();
            if (CurrentCategory is not null)
            {
                if (CurrentSetting is not null && ReferenceEquals(CurrentSetting.Category, CurrentCategory))
                    inSameCategory = true;
                if (CurrentCategory.Parent is not null) // Add the parent
                {
                    var listItem = PoliciesList.Items.Add("Up: " + CurrentCategory.Parent.DisplayName);
                    listItem.Name = "Up"; // Marks this row so it stays pinned first when the list is sorted
                    listItem.Tag = CurrentCategory.Parent;
                    listItem.ImageIndex = 6; // Up arrow
                    listItem.SubItems.Add("Parent");
                }
                foreach (var category in CurrentCategory.Children.Where(ShouldShowCategory).OrderBy(c => c.DisplayName)) // Add subcategories
                {
                    var listItem = PoliciesList.Items.Add(category.DisplayName);
                    listItem.Tag = category;
                    listItem.ImageIndex = GetImageIndexForCategory(category);
                }
                foreach (var policy in CurrentCategory.Policies.Where(ShouldShowPolicy).OrderBy(p => p.DisplayName)) // Add policies
                    AddPolicyListItem(policy);
                if (topItemIndex.HasValue & inSameCategory) // Minimize the list view's jumping around when refreshing
                {
                    if (PoliciesList.Items.Count > topItemIndex.Value)
                        PoliciesList.TopItem = PoliciesList.Items[topItemIndex.Value];
                }
                if (CategoriesTree.SelectedNode is null || !ReferenceEquals(CategoriesTree.SelectedNode.Tag, CurrentCategory)) // Update the tree view
                {
                    CategoriesTree.SelectedNode = CategoryNodes[CurrentCategory];
                }
            }
        }
        private void UpdateFavoritesListing()
        {
            // Sourced from FavoriteIds instead of a category - IDs no longer present in the
            // current workspace are silently skipped rather than erroring
            PoliciesList.Items.Clear();
            var favoritePolicies = FavoriteIds
                .Select(id => AdmxWorkspace.Policies.TryGetValue(id, out var policy) ? policy : null)
                .Where(p => p is not null)
                .OrderBy(p => p.DisplayName);
            foreach (var policy in favoritePolicies)
                AddPolicyListItem(policy);
        }
        // Adds one policy row to PoliciesList, keeping it selected if it's the current setting.
        // Shared by UpdateCategoryListing's policy loop and UpdateFavoritesListing.
        private void AddPolicyListItem(PolicyPlusPolicy policy)
        {
            var listItem = PoliciesList.Items.Add(policy.DisplayName);
            listItem.Tag = policy;
            listItem.ImageIndex = GetImageIndexForSetting(policy);
            listItem.SubItems.Add(GetPolicyState(policy));
            listItem.SubItems.Add(GetPolicyCommentText(policy));
            listItem.SubItems.Add(policy.UniqueID);
            if (ReferenceEquals(policy, CurrentSetting))
            {
                listItem.Selected = true;
                listItem.Focused = true;
                listItem.EnsureVisible();
            }
        }
        private void PoliciesList_ColumnClick(object sender, ColumnClickEventArgs e)
        {
            if (_sortColumn == e.Column)
                _sortAscending = !_sortAscending; // Repeat click on the same column reverses direction
            else
            {
                _sortColumn = e.Column;
                _sortAscending = true;
            }
            PoliciesList.ListViewItemSorter = new PolicyListSorter(_sortColumn, _sortAscending);
            PoliciesList.Sort();
            UpdateSortIcons();
        }
        // WinForms ListView has no managed way to show a sort arrow on a column header - this uses
        // the native header control's own HDF_SORTUP/HDF_SORTDOWN flags, the same mechanism apps
        // like Explorer use, so it composes correctly with the header's normal (theme-aware) drawing.
        private const int LvmGetHeader = 0x101F;
        private const int HdmGetItem = 0x120B;
        private const int HdmSetItem = 0x120C;
        private const int HdiFormat = 0x0004;
        private const int HdfSortUp = 0x0400;
        private const int HdfSortDown = 0x0200;
        private void UpdateSortIcons()
        {
            IntPtr headerHandle = PInvoke.SendMessage(PoliciesList.Handle, LvmGetHeader, IntPtr.Zero, IntPtr.Zero);
            if (headerHandle == IntPtr.Zero) return;
            for (int i = 0; i < PoliciesList.Columns.Count; i++)
            {
                var item = new PInvokeHdItem { Mask = HdiFormat };
                PInvoke.SendMessageHdItem(headerHandle, HdmGetItem, (IntPtr)i, ref item);
                item.Fmt &= ~(HdfSortUp | HdfSortDown);
                if (i == _sortColumn)
                    item.Fmt |= _sortAscending ? HdfSortUp : HdfSortDown;
                PInvoke.SendMessageHdItem(headerHandle, HdmSetItem, (IntPtr)i, ref item);
            }
        }
        // Sorts by the clicked column's text, but only within the existing Up-row / category / policy
        // grouping (matching how UpdateCategoryListing already orders the list), so sorting never
        // interleaves categories and policies.
        private class PolicyListSorter : System.Collections.IComparer
        {
            private readonly int _column;
            private readonly bool _ascending;
            public PolicyListSorter(int column, bool ascending)
            {
                _column = column;
                _ascending = ascending;
            }
            private static int Rank(ListViewItem item)
            {
                if (item.Name == "Up") return 0;
                return item.Tag is PolicyPlusCategory ? 1 : 2;
            }
            public int Compare(object x, object y)
            {
                var itemX = (ListViewItem)x;
                var itemY = (ListViewItem)y;
                int rankX = Rank(itemX), rankY = Rank(itemY);
                if (rankX != rankY) return rankX.CompareTo(rankY);
                string textX = _column < itemX.SubItems.Count ? itemX.SubItems[_column].Text : "";
                string textY = _column < itemY.SubItems.Count ? itemY.SubItems[_column].Text : "";
                int result = string.Compare(textX, textY, StringComparison.CurrentCultureIgnoreCase);
                return _ascending ? result : -result;
            }
        }
        public void UpdatePolicyInfo()
        {
            // Update the middle pane with the selected object's information
            bool hasCurrentSetting = CurrentSetting is not null | HighlightCategory is not null | CurrentCategory is not null;
            PolicyTitleLabel.Visible = hasCurrentSetting;
            PolicySupportedLabel.Visible = hasCurrentSetting;
            if (CurrentSetting is not null)
            {
                PolicyTitleLabel.Text = CurrentSetting.DisplayName;
                if (CurrentSetting.SupportedOn is null)
                {
                    PolicySupportedLabel.Text = "(no requirements information)";
                }
                else
                {
                    PolicySupportedLabel.Text = "Requirements:" + Constants.vbCrLf + CurrentSetting.SupportedOn.DisplayName;
                }
                PolicyDescLabel.Text = PrettifyDescription(CurrentSetting.DisplayExplanation);
                PolicyIsPrefTable.Visible = IsPreference(CurrentSetting);
            }
            else if (HighlightCategory is not null | CurrentCategory is not null)
            {
                var shownCategory = HighlightCategory ?? CurrentCategory;
                PolicyTitleLabel.Text = shownCategory.DisplayName;
                PolicySupportedLabel.Text = (HighlightCategory is null ? "This" : "The selected") + " category contains " + shownCategory.Policies.Count + " policies and " + shownCategory.Children.Count + " subcategories.";
                PolicyDescLabel.Text = PrettifyDescription(shownCategory.DisplayExplanation);
                PolicyIsPrefTable.Visible = false;
            }
            else
            {
                PolicyDescLabel.Text = "Select an item to see its description.";
                PolicyIsPrefTable.Visible = false;
            }
            SettingInfoPanel_ClientSizeChanged(null, null);
        }
        public bool IsOrphanCategory(PolicyPlusCategory Category)
        {
            return Category.Parent is null & !string.IsNullOrEmpty(Category.RawCategory.ParentID);
        }
        public bool IsEmptyCategory(PolicyPlusCategory Category)
        {
            return Category.Children.Count == 0 & Category.Policies.Count == 0;
        }
        public int GetImageIndexForCategory(PolicyPlusCategory Category)
        {
            if (IsOrphanCategory(Category))
            {
                return 1; // Orphaned
            }
            else if (IsEmptyCategory(Category))
            {
                return 2; // Empty
            }
            else
            {
                return 0;
            } // Normal
        }
        public int GetImageIndexForSetting(PolicyPlusPolicy Setting)
        {
            if (IsPreference(Setting))
            {
                return 7; // Preference, not policy (exclamation mark)
            }
            else if (Setting.RawPolicy.Elements is null || Setting.RawPolicy.Elements.Count == 0)
            {
                return 4; // Normal
            }
            else
            {
                return 5;
            } // Extra configuration
        }
        public bool ShouldShowCategory(PolicyPlusCategory Category) => ShouldShowCategoryCore(Category, null);

        // Same logic as ShouldShowCategory, but can memoize subtree results for the duration of one tree walk
        // (e.g. PopulateAdmxUi's addCategory) so a category's visibility isn't recomputed once per ancestor level.
        // Cache is never persisted across calls, so there's no invalidation to get wrong.
        private bool ShouldShowCategoryCore(PolicyPlusCategory Category, Dictionary<PolicyPlusCategory, bool> Cache)
        {
            // Should this category be shown considering the current filter?
            if (ViewEmptyCategories)
            {
                return true;
            }
            if (Cache is not null && Cache.TryGetValue(Category, out bool cached))
            {
                return cached;
            }
            bool result = Category.Policies.Any(ShouldShowPolicy) || Category.Children.Any(c => ShouldShowCategoryCore(c, Cache));
            if (Cache is not null)
            {
                Cache[Category] = result;
            }
            return result;
        }
        public bool ShouldShowPolicy(PolicyPlusPolicy Policy)
        {
            // Should this policy be shown considering the current filter and active sections?
            if (!PolicyVisibleInSection(Policy, ViewPolicyTypes))
                return false;
            if (SearchMatcher is not null && !SearchMatcher(Policy))
                return false;
            if (ViewFilteredOnly)
            {
                if ((int)(ViewPolicyTypes & AdmxPolicySection.Machine) > 0 & PolicyVisibleInSection(Policy, AdmxPolicySection.Machine))
                {
                    if (IsPolicyVisibleAfterFilter(Policy, false))
                        return true;
                }
                if ((int)(ViewPolicyTypes & AdmxPolicySection.User) > 0 & PolicyVisibleInSection(Policy, AdmxPolicySection.User))
                {
                    if (IsPolicyVisibleAfterFilter(Policy, true))
                        return true;
                }
                return false;
            }
            else
            {
                return true;
            }
        }
        public void MoveToVisibleCategoryAndReload()
        {
            // Move up in the categories tree until a visible one is found
            var newFocusCategory = CurrentCategory;
            var newFocusPolicy = CurrentSetting;
            while (!(newFocusCategory is null) && !ShouldShowCategory(newFocusCategory))
            {
                newFocusCategory = newFocusCategory.Parent;
                newFocusPolicy = null;
            }
            if (newFocusPolicy is not null && !ShouldShowPolicy(newFocusPolicy))
                newFocusPolicy = null;
            PopulateAdmxUi();
            CurrentCategory = newFocusCategory;
            UpdateCategoryListing();
            CurrentSetting = newFocusPolicy;
            UpdatePolicyInfo();
        }
        public string GetPolicyState(PolicyPlusPolicy Policy)
        {
            // Get a human-readable string describing the status of a policy, considering all active sections
            if (ViewPolicyTypes == AdmxPolicySection.Both)
            {
                string userState = GetPolicyState(Policy, AdmxPolicySection.User);
                string machState = GetPolicyState(Policy, AdmxPolicySection.Machine);
                var section = Policy.RawPolicy.Section;
                if (section == AdmxPolicySection.Both)
                {
                    if ((userState ?? "") == (machState ?? ""))
                    {
                        return userState + " (2)";
                    }
                    else if (userState == "Not Configured")
                    {
                        return machState + " (C)";
                    }
                    else if (machState == "Not Configured")
                    {
                        return userState + " (U)";
                    }
                    else
                    {
                        return "Mixed";
                    }
                }
                else if (section == AdmxPolicySection.Machine)
                    return machState + " (C)";
                else
                    return userState + " (U)";
            }
            else
            {
                return GetPolicyState(Policy, ViewPolicyTypes);
            }
        }
        public string GetPolicyState(PolicyPlusPolicy Policy, AdmxPolicySection Section)
        {
            // Get the human-readable status of a policy considering only one section
            switch (PolicyProcessing.GetPolicyState(Section == AdmxPolicySection.Machine ? CompPolicySource : UserPolicySource, Policy))
            {
                case PolicyState.Disabled:
                    {
                        return "Disabled";
                    }
                case PolicyState.Enabled:
                    {
                        return "Enabled";
                    }
                case PolicyState.NotConfigured:
                    {
                        return "Not Configured";
                    }

                default:
                    {
                        return "Unknown";
                    }
            }
        }
        public string GetPolicyCommentText(PolicyPlusPolicy Policy)
        {
            // Get the comment text to show in the Comment column, considering all active sections
            if (ViewPolicyTypes == AdmxPolicySection.Both)
            {
                string userComment = GetPolicyComment(Policy, AdmxPolicySection.User);
                string compComment = GetPolicyComment(Policy, AdmxPolicySection.Machine);
                if (string.IsNullOrEmpty(userComment) & string.IsNullOrEmpty(compComment))
                {
                    return "";
                }
                else if (!string.IsNullOrEmpty(userComment) & !string.IsNullOrEmpty(compComment))
                {
                    return "(multiple)";
                }
                else if (!string.IsNullOrEmpty(userComment))
                {
                    return userComment;
                }
                else
                {
                    return compComment;
                }
            }
            else
            {
                return GetPolicyComment(Policy, ViewPolicyTypes);
            }
        }
        public string GetPolicyComment(PolicyPlusPolicy Policy, AdmxPolicySection Section)
        {
            // Get a policy's comment in one section
            var commentSource = Section == AdmxPolicySection.Machine ? CompComments : UserComments;
            if (commentSource is null)
            {
                return "";
            }
            else if (commentSource.ContainsKey(Policy.UniqueID))
                return commentSource[Policy.UniqueID];
            else
                return "";
        }
        public bool IsPreference(PolicyPlusPolicy Policy)
        {
            return !string.IsNullOrEmpty(Policy.RawPolicy.RegistryKey) & !RegistryPolicyProxy.IsPolicyKey(Policy.RawPolicy.RegistryKey);
        }
        public void ShowSettingEditor(PolicyPlusPolicy Policy, AdmxPolicySection Section)
        {
            // Show the Edit Policy Setting dialog for a policy and reload if changes were made
            if (My.MyProject.Forms.EditSetting.PresentDialog(Policy, Section, AdmxWorkspace, CompPolicySource, UserPolicySource, CompPolicyLoader, UserPolicyLoader, CompComments, UserComments) == DialogResult.OK)
            {
                _isDirty = true;
                // Keep the selection where it is if possible
                if (CurrentCategory is null || ShouldShowCategory(CurrentCategory))
                    UpdateCategoryListing();
                else
                    MoveToVisibleCategoryAndReload();
            }
        }
        public void ClearSelections()
        {
            CurrentSetting = null;
            HighlightCategory = null;
        }
        public void OpenPolicyLoaders(PolicyLoader User, PolicyLoader Computer, bool Quiet)
        {
            // Create policy sources from the given loaders
            if (CompPolicyLoader is not null | UserPolicyLoader is not null)
                ClosePolicySources();
            UserPolicyLoader = User;
            UserPolicySource = User.OpenSource();
            CompPolicyLoader = Computer;
            CompPolicySource = Computer.OpenSource();
            bool allOk = true;
            string policyStatus(PolicyLoader Loader) { switch (Loader.GetWritability()) { case PolicySourceWritability.Writable: { return "is fully writable"; } case PolicySourceWritability.NoCommit: { allOk = false; return "cannot be saved"; } default: { allOk = false; return "cannot be modified"; } } }; // No writing
            Dictionary<string, string> loadComments(PolicyLoader Loader)
            {
                string cmtxPath = Loader.GetCmtxPath();
                if (string.IsNullOrEmpty(cmtxPath))
                {
                    return null;
                }
                else
                {
                    try
                    {
                        System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(cmtxPath));
                        if (System.IO.File.Exists(cmtxPath))
                        {
                            return CmtxFile.Load(cmtxPath).ToCommentTable();
                        }
                        else
                        {
                            return new Dictionary<string, string>();
                        }
                    }
                    catch (Exception ex)
                    {
                        return null;
                    }
                }
            };
            string userStatus = policyStatus(User);
            string compStatus = policyStatus(Computer);
            UserComments = loadComments(User);
            CompComments = loadComments(Computer);
            UserSourceLabel.Text = UserPolicyLoader.GetDisplayInfo();
            ComputerSourceLabel.Text = CompPolicyLoader.GetDisplayInfo();
            if (allOk)
            {
                if (!Quiet)
                {
                    MsgBoxCompat.Show("Both the user and computer policy sources are loaded and writable.", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
            }
            else
            {
                string msgText = "Not all policy sources are fully writable." + Constants.vbCrLf + Constants.vbCrLf + "The user source " + userStatus + "." + Constants.vbCrLf + Constants.vbCrLf + "The computer source " + compStatus + ".";
                MsgBoxCompat.Show(msgText, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }
        public void ClosePolicySources()
        {
            // Clean up the policy sources
            bool allOk = true;
            if (UserPolicyLoader is not null)
            {
                if (!UserPolicyLoader.Close())
                    allOk = false;
            }
            if (CompPolicyLoader is not null)
            {
                if (!CompPolicyLoader.Close())
                    allOk = false;
            }
            if (!allOk)
            {
                MsgBoxCompat.Show("Cleanup did not complete fully because the loaded resources are open in other programs.", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }
        public void ShowSearchDialog(Func<PolicyPlusPolicy, bool> Searcher)
        {
            // Show the search dialog and make it start a search if appropriate
            DialogResult result;
            if (Searcher is null)
            {
                result = My.MyProject.Forms.FindResults.PresentDialog();
            }
            else
            {
                result = My.MyProject.Forms.FindResults.PresentDialogStartSearch(AdmxWorkspace, Searcher);
            }
            if (result == DialogResult.OK)
            {
                var selPol = My.MyProject.Forms.FindResults.SelectedPolicy;
                ShowSettingEditor(selPol, ViewPolicyTypes);
                FocusPolicy(selPol);
            }
        }
        public void ClearAdmxWorkspace()
        {
            // Clear out all the per-workspace bookkeeping
            AdmxWorkspace = new AdmxBundle();
            My.MyProject.Forms.FindResults.ClearSearch();
        }
        public void FocusPolicy(PolicyPlusPolicy Policy)
        {
            // Try to automatically select a policy in the list view
            if (CategoryNodes.ContainsKey(Policy.Category))
            {
                CurrentCategory = Policy.Category;
                UpdateCategoryListing();
                foreach (ListViewItem entry in PoliciesList.Items)
                {
                    if (ReferenceEquals(entry.Tag, Policy))
                    {
                        entry.Selected = true;
                        entry.Focused = true;
                        entry.EnsureVisible();
                        break;
                    }
                }
            }
        }
        public bool IsPolicyVisibleAfterFilter(PolicyPlusPolicy Policy, bool IsUser)
        {
            // Calculate whether a policy is visible with the current filter
            if (CurrentFilter.ManagedPolicy.HasValue)
            {
                if (IsPreference(Policy) == CurrentFilter.ManagedPolicy.Value)
                    return false;
            }
            if (CurrentFilter.PolicyState.HasValue)
            {
                var policyState = PolicyProcessing.GetPolicyState(IsUser ? UserPolicySource : CompPolicySource, Policy);
                switch (CurrentFilter.PolicyState.Value)
                {
                    case FilterPolicyState.Configured:
                        {
                            if (policyState == PolicyState.NotConfigured)
                                return false;
                            break;
                        }
                    case FilterPolicyState.NotConfigured:
                        {
                            if (policyState != PolicyState.NotConfigured)
                                return false;
                            break;
                        }
                    case FilterPolicyState.Disabled:
                        {
                            if (policyState != PolicyState.Disabled)
                                return false;
                            break;
                        }
                    case FilterPolicyState.Enabled:
                        {
                            if (policyState != PolicyState.Enabled)
                                return false;
                            break;
                        }
                }
            }
            if (CurrentFilter.Commented.HasValue)
            {
                var commentDict = IsUser ? UserComments : CompComments;
                if ((commentDict.ContainsKey(Policy.UniqueID) && !string.IsNullOrEmpty(commentDict[Policy.UniqueID])) != CurrentFilter.Commented.Value)
                    return false;
            }
            if (CurrentFilter.AllowedProducts is not null)
            {
                if (!PolicyProcessing.IsPolicySupported(Policy, CurrentFilter.AllowedProducts, CurrentFilter.AlwaysMatchAny, CurrentFilter.MatchBlankSupport))
                    return false;
            }
            return true;
        }
        public bool PolicyVisibleInSection(PolicyPlusPolicy Policy, AdmxPolicySection Section)
        {
            // Does this policy apply to the given section?
            return (int)(Policy.RawPolicy.Section & Section) > 0;
        }
        public PolFile GetOrCreatePolFromPolicySource(IPolicySource Source)
        {
            if (Source is PolFile)
            {
                // If it's already a POL, just save it
                return (PolFile)Source;
            }
            else if (Source is RegistryPolicyProxy)
            {
                // Recurse through the Registry branch and create a POL
                var regRoot = ((RegistryPolicyProxy)Source).EncapsulatedRegistry;
                var pol = new PolFile();
                void addSubtree(string PathRoot, RegistryKey Key)
                    {
                        foreach (var valName in Key.GetValueNames())
                        {
                            var valData = Key.GetValue(valName, null, RegistryValueOptions.DoNotExpandEnvironmentNames);
                            // Reinterpret signed Int32/Int64 as unsigned, matching RegistryPolicyProxy.GetValue,
                            // so DWORD/QWORD values with the high bit set (e.g. 0xFFFFFFFF) don't overflow when saved
                            if (valData is int i)
                            {
                                valData = new ReinterpretableDword { Signed = i }.Unsigned;
                            }
                            else if (valData is long l)
                            {
                                valData = new ReinterpretableQword { Signed = l }.Unsigned;
                            }
                            pol.SetValue(PathRoot, valName, valData, Key.GetValueKind(valName));
                        }
                        foreach (var subkeyName in Key.GetSubKeyNames())
                        {
                            using (var subkey = Key.OpenSubKey(subkeyName, false))
                            {
                                addSubtree(PathRoot + @"\" + subkeyName, subkey);
                            }
                        }
                    }
                foreach (var policyPath in RegistryPolicyProxy.PolicyKeys)
                {
                    using (var policyKey = regRoot.OpenSubKey(policyPath, false))
                    {
                        addSubtree(policyPath, policyKey);
                    }
                }
                return pol;
            }
            else
            {
                throw new InvalidOperationException("Policy source type not supported");
            }
        }
        public DialogResult DisplayAdmxLoadErrorReport(IEnumerable<AdmxLoadFailure> Failures, bool AskContinue = false)
        {
            if (Failures.Count() == 0)
                return DialogResult.OK;
            var boxButtons = AskContinue ? MessageBoxButtons.YesNo : MessageBoxButtons.OK;
            string header = "Errors were encountered while adding administrative templates to the workspace.";
            return MsgBoxCompat.Show(header + (AskContinue ? " Continue trying to use this workspace?" : "") + Constants.vbCrLf + Constants.vbCrLf + string.Join(Constants.vbCrLf + Constants.vbCrLf, Failures.Select(f => f.ToString())), boxButtons, MessageBoxIcon.Exclamation);
        }
        public string GetPreferredLanguageCode()
        {
            return Conversions.ToString(Configuration.GetValue("LanguageCode", System.Globalization.CultureInfo.CurrentCulture.Name));
        }
        private void CategoriesTree_AfterSelect(object sender, TreeViewEventArgs e)
        {
            // When the user selects a new category in the left pane
            CurrentCategory = ReferenceEquals(e.Node, FavoritesNode) ? null : (PolicyPlusCategory)e.Node.Tag;
            UpdateCategoryListing();
            ClearSelections();
            UpdatePolicyInfo();
        }
        private void CategoriesTree_DrawNode(object sender, DrawTreeNodeEventArgs e)
        {
            // The themed selection highlight the TreeView draws by default ignores TreeNode.BackColor
            // and, when the tree doesn't have focus (HideSelection = false keeps the selected node
            // visible even then), renders as a low-contrast grey that's nearly invisible in dark mode.
            // Owner-drawing just the text background sidesteps that theme-controlled rendering with an
            // explicit, theme-appropriate color instead.
            bool selected = e.Node.IsSelected;
            Color back = selected ? SelectionBackColor : CategoriesTree.BackColor;
            Color fore = selected ? SelectionForeColor : CategoriesTree.ForeColor;
            using (var brush = new SolidBrush(back))
                e.Graphics.FillRectangle(brush, e.Bounds);
            TextRenderer.DrawText(e.Graphics, e.Node.Text, CategoriesTree.Font, e.Bounds, fore, back, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.NoPrefix);
        }
        private void SearchTextbox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                e.SuppressKeyPress = true; // Stop the Enter from also triggering a system beep
                RunSearch();
            }
            else if (e.KeyCode == Keys.Escape)
            {
                ClearSearch();
            }
        }
        private void SearchButton_Click(object sender, EventArgs e)
        {
            RunSearch();
        }
        private void ClearSearchButton_Click(object sender, EventArgs e)
        {
            ClearSearch();
        }
        private void ClearSearch()
        {
            SearchTextbox.Text = "";
            RunSearch();
        }
        private void RunSearch()
        {
            // Runs only on demand (Enter or the search button), not on every keystroke - see
            // the search-behavior discussion in the plan file for why this replaced live filtering
            string query = SearchTextbox.Text;
            SearchMatcher = string.IsNullOrWhiteSpace(query) ? null : PolicySearch.BuildMatcher(PolicySearch.ToSubstringQuery(query), true, true, true, true, true, CompComments, UserComments);
            MoveToVisibleCategoryAndReload();
        }
        private void ResizePolicyNameColumn(object sender, EventArgs e)
        {
            // Fit the policy name column to the window size, but capped - letting it fill 100% of
            // whatever's left (the old behavior) wastes huge amounts of space on wide windows and,
            // since it didn't know about the ID column, pushed ID off the visible area entirely
            if (IsHandleCreated)
                BeginInvoke(() =>
                {
                    int fixedColumnsWidth = PoliciesList.Columns[1].Width + PoliciesList.Columns[2].Width + PoliciesList.Columns[3].Width;
                    int available = PoliciesList.ClientSize.Width - fixedColumnsWidth;
                    PoliciesList.Columns[0].Width = Math.Max(150, Math.Min(available, 320));
                });
        }
        private void PoliciesList_DrawColumnHeader(object sender, DrawListViewColumnHeaderEventArgs e) => e.DrawDefault = true;
        private void PoliciesList_DrawItem(object sender, DrawListViewItemEventArgs e)
        {
            // Actual drawing happens per-cell in DrawSubItem (Details view calls that for every
            // column, including column 0), so there's nothing to do here beyond opting out of the
            // default item-level drawing.
        }
        private void PoliciesList_DrawSubItem(object sender, DrawListViewSubItemEventArgs e)
        {
            // Same theming issue as CategoriesTree_DrawNode: the default selection highlight ignores
            // ListViewItem.BackColor and is nearly invisible in dark mode when the list isn't focused.
            bool selected = e.Item.Selected;
            Color back = selected ? SelectionBackColor : PoliciesList.BackColor;
            Color fore = selected ? SelectionForeColor : PoliciesList.ForeColor;
            using (var brush = new SolidBrush(back))
                e.Graphics.FillRectangle(brush, e.Bounds);
            int textLeft = e.Bounds.Left + 2;
            if (e.ColumnIndex == 0 && e.Item.ImageIndex >= 0 && PoliciesList.SmallImageList is not null)
            {
                var image = PoliciesList.SmallImageList.Images[e.Item.ImageIndex];
                e.Graphics.DrawImage(image, textLeft, e.Bounds.Top + (e.Bounds.Height - image.Height) / 2);
                textLeft += image.Width + 4;
            }
            var textRect = new Rectangle(textLeft, e.Bounds.Top, e.Bounds.Right - textLeft, e.Bounds.Height);
            TextRenderer.DrawText(e.Graphics, e.SubItem.Text, PoliciesList.Font, textRect, fore, back, TextFormatFlags.VerticalCenter | TextFormatFlags.Left | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }
        private void PoliciesList_SelectedIndexChanged(object sender, EventArgs e)
        {
            // When the user highlights an item in the right pane
            if (PoliciesList.SelectedItems.Count > 0)
            {
                var selObject = PoliciesList.SelectedItems[0].Tag;
                if (selObject is PolicyPlusPolicy)
                {
                    CurrentSetting = (PolicyPlusPolicy)selObject;
                    HighlightCategory = null;
                }
                else if (selObject is PolicyPlusCategory)
                {
                    HighlightCategory = (PolicyPlusCategory)selObject;
                    CurrentSetting = null;
                }
            }
            else
            {
                ClearSelections();
            }
            UpdatePolicyInfo();
        }
        private void ExitToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Close();
        }
        private void OpenADMXFolderToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Show the Open ADMX Folder dialog and load the policy definitions
            if (My.MyProject.Forms.OpenAdmxFolder.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    if (My.MyProject.Forms.OpenAdmxFolder.ClearWorkspace)
                        ClearAdmxWorkspace();
                    DisplayAdmxLoadErrorReport(AdmxWorkspace.LoadFolder(My.MyProject.Forms.OpenAdmxFolder.SelectedFolder, GetPreferredLanguageCode()));
                    // Only update the last source when successfully opening a complete source
                    if (My.MyProject.Forms.OpenAdmxFolder.ClearWorkspace)
                        Configuration.SetValue("AdmxSource", My.MyProject.Forms.OpenAdmxFolder.SelectedFolder);
                }
                catch (Exception ex)
                {
                    MsgBoxCompat.Show("The folder could not be fully added to the workspace. " + ex.Message, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                }
                PopulateAdmxUi();
            }
        }
        private void OpenADMXFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Open a single ADMX file
            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter = "Policy definitions files|*.admx";
                ofd.Title = "Open ADMX file";
                if (ofd.ShowDialog() != DialogResult.OK)
                    return;
                try
                {
                    DisplayAdmxLoadErrorReport(AdmxWorkspace.LoadFile(ofd.FileName, GetPreferredLanguageCode()));
                }
                catch (Exception ex)
                {
                    MsgBoxCompat.Show("The ADMX file could not be added to the workspace. " + ex.Message, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                }
                PopulateAdmxUi();
            }
        }
        private void CloseADMXWorkspaceToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Close all policy definitions and clear the workspace
            ClearAdmxWorkspace();
            PopulateAdmxUi();
        }
        private void EmptyCategoriesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Toggle whether empty categories are visible
            ViewEmptyCategories = !ViewEmptyCategories;
            EmptyCategoriesToolStripMenuItem.Checked = ViewEmptyCategories;
            MoveToVisibleCategoryAndReload();
        }
        private void ComboAppliesTo_SelectedIndexChanged(object sender, EventArgs e)
        {
            // When the user chooses a different section to work with
            switch (ComboAppliesTo.Text ?? "")
            {
                case "User":
                    {
                        ViewPolicyTypes = AdmxPolicySection.User;
                        break;
                    }
                case "Computer":
                    {
                        ViewPolicyTypes = AdmxPolicySection.Machine;
                        break;
                    }

                default:
                    {
                        ViewPolicyTypes = AdmxPolicySection.Both;
                        break;
                    }
            }
            MoveToVisibleCategoryAndReload();
        }
        private void PoliciesList_DoubleClick(object sender, EventArgs e)
        {
            // When the user opens a policy object in the right pane
            if (PoliciesList.SelectedItems.Count == 0)
                return;
            var policyItem = PoliciesList.SelectedItems[0].Tag;
            if (policyItem is PolicyPlusCategory)
            {
                CurrentCategory = (PolicyPlusCategory)policyItem;
                UpdateCategoryListing();
            }
            else if (policyItem is PolicyPlusPolicy)
            {
                ShowSettingEditor((PolicyPlusPolicy)policyItem, ViewPolicyTypes);
            }
        }
        private void DeduplicatePoliciesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Make otherwise-identical pairs of user and computer policies into single dual-section policies
            ClearSelections();
            int deduped = PolicyProcessing.DeduplicatePolicies(AdmxWorkspace);
            if (deduped > 0)
                _isDirty = true;
            MsgBoxCompat.Show("Deduplicated " + deduped + " policies.", MessageBoxButtons.OK, MessageBoxIcon.Information);
            UpdateCategoryListing();
            UpdatePolicyInfo();
        }
        private void FindByIDToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Show the Find By ID window and try to move to the selected object
            My.MyProject.Forms.FindById.AdmxWorkspace = AdmxWorkspace;
            if (My.MyProject.Forms.FindById.ShowDialog() == DialogResult.OK)
            {
                var selCat = My.MyProject.Forms.FindById.SelectedCategory;
                var selPol = My.MyProject.Forms.FindById.SelectedPolicy;
                var selPro = My.MyProject.Forms.FindById.SelectedProduct;
                var selSup = My.MyProject.Forms.FindById.SelectedSupport;
                if (selCat is not null)
                {
                    if (CategoryNodes.ContainsKey(selCat))
                    {
                        CurrentCategory = selCat;
                        UpdateCategoryListing();
                    }
                    else
                    {
                        MsgBoxCompat.Show("The category is not currently visible. Change the view settings and try again.", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    }
                }
                else if (selPol is not null)
                {
                    ShowSettingEditor(selPol, (AdmxPolicySection)Math.Min((int)ViewPolicyTypes, (int)My.MyProject.Forms.FindById.SelectedSection));
                    FocusPolicy(selPol);
                }
                else if (selPro is not null)
                {
                    My.MyProject.Forms.DetailProduct.PresentDialog(selPro);
                }
                else if (selSup is not null)
                {
                    My.MyProject.Forms.DetailSupport.PresentDialog(selSup);
                }
                else
                {
                    MsgBoxCompat.Show("That object could not be found.", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                }
            }
        }
        private void OpenPolicyResourcesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Show the Open Policy Resources dialog and open its loaders
            if (My.MyProject.Forms.OpenPol.ShowDialog() == DialogResult.OK)
            {
                OpenPolicyLoaders(My.MyProject.Forms.OpenPol.SelectedUser, My.MyProject.Forms.OpenPol.SelectedComputer, false);
                MoveToVisibleCategoryAndReload();
            }
        }
        private void OpenREGFileToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Import a REG file into a fresh, standalone in-memory source (not merged into the
            // currently-open source), so it can be edited and exported in isolation - same idea
            // as opening a POL file, just for REG. The section (Computer/User) is detected from
            // the file's own key headers rather than asked up front.
            RegFile reg;
            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter = "Registry scripts|*.reg";
                if (ofd.ShowDialog() != DialogResult.OK)
                    return;
                try
                {
                    reg = RegFile.Load(ofd.FileName, "");
                }
                catch (Exception ex)
                {
                    MsgBoxCompat.Show("Failed to load the REG file.", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    return;
                }
            }
            var hiveCounts = reg.CountKeysByHive();
            bool hasComputer = hiveCounts[RegFileHive.Computer] > 0;
            bool hasUser = hiveCounts[RegFileHive.User] > 0;
            if (!hasComputer && !hasUser)
            {
                MsgBoxCompat.Show("This REG file doesn't contain any Computer (HKEY_LOCAL_MACHINE) or User (HKEY_CURRENT_USER/HKEY_USERS) entries to import.", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            RegFileHive chosenHive;
            if (hasComputer && hasUser)
            {
                // Mixed-hive file - ask which single hive to keep rather than silently picking one
                string msg = "This REG file mixes Computer and User entries (" + hiveCounts[RegFileHive.Computer] + " Computer key(s), " + hiveCounts[RegFileHive.User] + " User key(s)). Only one can be opened at a time this way." + Constants.vbCrLf + Constants.vbCrLf + "Click Yes to import the Computer entries (discarding the User entries), or No to import the User entries (discarding the Computer entries).";
                var result = MsgBoxCompat.Show(msg, MessageBoxButtons.YesNoCancel, MessageBoxIcon.Warning);
                if (result == DialogResult.Cancel)
                    return;
                chosenHive = result == DialogResult.Yes ? RegFileHive.Computer : RegFileHive.User;
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
                UserPolicyLoader?.Close(); // Release whatever the old loader held (e.g. an NtUserDat mount) before replacing it
                UserPolicyLoader = newLoader;
                UserPolicySource = pol;
                UserComments = new Dictionary<string, string>();
                UserSourceLabel.Text = UserPolicyLoader.GetDisplayInfo();
            }
            else
            {
                CompPolicyLoader?.Close();
                CompPolicyLoader = newLoader;
                CompPolicySource = pol;
                CompComments = new Dictionary<string, string>();
                ComputerSourceLabel.Text = CompPolicyLoader.GetDisplayInfo();
            }
            _isDirty = true;
            ClearSelections();
            MoveToVisibleCategoryAndReload();
            string successMsg = "REG file opened as a standalone editable " + (isUser ? "User" : "Computer") + " source. Use Export POL/REG to save it - the normal Save Policies action discards scratch sources like this one.";
            if (reg.HasDefaultValues())
                successMsg = "This REG file set one or more keys' default values, which Group Policy has no way to represent - those specific entries were skipped." + Constants.vbCrLf + Constants.vbCrLf + successMsg;
            MsgBoxCompat.Show(successMsg, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        private void SavePoliciesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Save policy state and comments to disk
            // Doesn't matter, it's just comments
            void saveComments(Dictionary<string, string> Comments, PolicyLoader Loader) { try { if (Comments is not null) CmtxFile.FromCommentTable(Comments).Save(Loader.GetCmtxPath()); } catch (Exception ex) { } };
            saveComments(UserComments, UserPolicyLoader);
            saveComments(CompComments, CompPolicyLoader);
            try
            {
                string compStatus = "not writable";
                string userStatus = "not writable";
                if (CompPolicyLoader.GetWritability() == PolicySourceWritability.Writable)
                    compStatus = CompPolicyLoader.Save();
                if (UserPolicyLoader.GetWritability() == PolicySourceWritability.Writable)
                    userStatus = UserPolicyLoader.Save();
                Configuration.SetValue("CompSourceType", (int)CompPolicyLoader.Source);
                Configuration.SetValue("UserSourceType", (int)UserPolicyLoader.Source);
                Configuration.SetValue("CompSourceData", CompPolicyLoader.LoaderData ?? "");
                Configuration.SetValue("UserSourceData", UserPolicyLoader.LoaderData ?? "");
                _isDirty = false;
                MsgBoxCompat.Show("Success." + Constants.vbCrLf + Constants.vbCrLf + "User policies: " + userStatus + "." + Constants.vbCrLf + Constants.vbCrLf + "Computer policies: " + compStatus + ".", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception ex)
            {
                MsgBoxCompat.Show("Saving failed!" + Constants.vbCrLf + Constants.vbCrLf + ex.Message, MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
            }
        }
        private void ResetAllToDefaultToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Reset every currently-configured policy back to Not Configured, across both Computer and User
            if (MsgBoxCompat.Show("This will reset every configured policy back to Not Configured, across both Computer and User policies. This cannot be undone. Continue?", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes)
                return;
            ClearSelections();
            int reset = 0;
            foreach (var policy in AdmxWorkspace.Policies.Values)
            {
                var section = policy.RawPolicy.Section;
                if (section == AdmxPolicySection.Both || section == AdmxPolicySection.Machine)
                {
                    if (PolicyProcessing.GetPolicyState(CompPolicySource, policy) != PolicyState.NotConfigured)
                    {
                        PolicyProcessing.ForgetPolicy(CompPolicySource, policy);
                        reset++;
                    }
                }
                if (section == AdmxPolicySection.Both || section == AdmxPolicySection.User)
                {
                    if (PolicyProcessing.GetPolicyState(UserPolicySource, policy) != PolicyState.NotConfigured)
                    {
                        PolicyProcessing.ForgetPolicy(UserPolicySource, policy);
                        reset++;
                    }
                }
            }
            if (reset > 0)
                _isDirty = true;
            MsgBoxCompat.Show("Reset " + reset + " policy configuration" + (reset == 1 ? "" : "s") + " to Not Configured.", MessageBoxButtons.OK, MessageBoxIcon.Information);
            UpdateCategoryListing();
            UpdatePolicyInfo();
        }
        private void AboutToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Show author and version information if it was compiled into the program
            string about = $"Policy Plus, maintained by Darren Milne, originally created by Ben Nordick.{Constants.vbCrLf}{Constants.vbCrLf}Available on GitHub: systmworks/PolicyPlus.";
            if (!string.IsNullOrEmpty(VersionHolder.AppVersion.Trim()))
                about += $" Version {VersionHolder.AppVersion.Trim()} (commit {VersionHolder.Version.Trim()}).";
            MsgBoxCompat.Show(about, MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        private void ByTextToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Show the Find By Text window and start the search
            if (My.MyProject.Forms.FindByText.PresentDialog(UserComments, CompComments) == DialogResult.OK)
            {
                ShowSearchDialog(My.MyProject.Forms.FindByText.Searcher);
            }
        }
        private void SearchResultsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Show the search results window but don't start a search
            ShowSearchDialog(null);
        }
        private void FindNextToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Move to the next policy in the search results
            do
            {
                var nextPol = My.MyProject.Forms.FindResults.NextPolicy();
                if (nextPol is null)
                {
                    MsgBoxCompat.Show("There are no more results that match the filter.", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    break;
                }
                else if (ShouldShowPolicy(nextPol))
                {
                    FocusPolicy(nextPol);
                    break;
                }
            }
            while (true);
        }
        private void ByRegistryToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Show the Find By Registry window and start the search
            if (My.MyProject.Forms.FindByRegistry.ShowDialog() == DialogResult.OK)
                ShowSearchDialog(My.MyProject.Forms.FindByRegistry.Searcher);
        }
        private void SettingInfoPanel_ClientSizeChanged(object sender, EventArgs e)
        {
            // Finagle the middle pane's UI elements
            SettingInfoPanel.AutoScrollMinSize = SettingInfoPanel.Size;
            PolicyTitleLabel.MaximumSize = new Size(PolicyInfoTable.Width, 0);
            PolicySupportedLabel.MaximumSize = new Size(PolicyInfoTable.Width, 0);
            PolicyDescLabel.MaximumSize = new Size(PolicyInfoTable.Width, 0);
            PolicyIsPrefLabel.MaximumSize = new Size(PolicyInfoTable.Width - 22, 0); // Leave room for the exclamation icon
            PolicyInfoTable.MaximumSize = new Size(SettingInfoPanel.Width - (SettingInfoPanel.VerticalScroll.Visible ? SystemInformation.VerticalScrollBarWidth : 0), 0);
            PolicyInfoTable.Width = PolicyInfoTable.MaximumSize.Width;
            if (PolicyInfoTable.ColumnCount > 0)
                PolicyInfoTable.ColumnStyles[0].Width = PolicyInfoTable.ClientSize.Width; // Only once everything is initialized
            PolicyInfoTable.PerformLayout(); // Force the table to take up its full desired size
            PInvoke.ShowScrollBar(SettingInfoPanel.Handle, 0, false); // 0 means horizontal
        }
        private void RestoreWindowBounds()
        {
            // No saved bounds yet (first run) unless all four are present
            int left = Conversions.ToInteger(Configuration.GetValue("WindowLeft", int.MinValue));
            int top = Conversions.ToInteger(Configuration.GetValue("WindowTop", int.MinValue));
            int width = Conversions.ToInteger(Configuration.GetValue("WindowWidth", 0));
            int height = Conversions.ToInteger(Configuration.GetValue("WindowHeight", 0));
            if (left == int.MinValue || top == int.MinValue || width <= 0 || height <= 0)
                return;
            var bounds = new Rectangle(left, top, width, height);
            // Guard against a saved position from a monitor/resolution that's since changed
            if (!Screen.AllScreens.Any(s => s.WorkingArea.IntersectsWith(bounds)))
                return;
            StartPosition = FormStartPosition.Manual;
            Bounds = bounds;
            if (Conversions.ToInteger(Configuration.GetValue("WindowMaximized", 0)) == 1)
                WindowState = FormWindowState.Maximized;
        }
        private void SaveWindowBounds()
        {
            if (WindowState == FormWindowState.Minimized)
                return; // Not a useful geometry to restore to; keep the last saved Normal/Maximized bounds
            bool maximized = WindowState == FormWindowState.Maximized;
            var bounds = maximized ? RestoreBounds : Bounds;
            Configuration.SetValue("WindowLeft", bounds.Left);
            Configuration.SetValue("WindowTop", bounds.Top);
            Configuration.SetValue("WindowWidth", bounds.Width);
            Configuration.SetValue("WindowHeight", bounds.Height);
            Configuration.SetValue("WindowMaximized", maximized ? 1 : 0);
        }
        private void RestorePaneLayout()
        {
            // Only apply a saved distance if it still fits the (possibly since-changed) window/monitor -
            // SplitContainer throws if SplitterDistance is set outside its current valid range
            int treeWidth = Conversions.ToInteger(Configuration.GetValue("TreePaneWidth", 0));
            if (treeWidth > 0 && treeWidth < SplitContainer.ClientSize.Width - SplitContainer.Panel2MinSize)
                SplitContainer.SplitterDistance = treeWidth;
            int descWidth = Conversions.ToInteger(Configuration.GetValue("DescriptionPaneWidth", 0));
            if (descWidth > 0 && descWidth < DescriptionSplitContainer.ClientSize.Width - DescriptionSplitContainer.Panel2MinSize)
                DescriptionSplitContainer.SplitterDistance = descWidth;
            // Column widths have no such constraint - any positive value is valid
            int stateWidth = Conversions.ToInteger(Configuration.GetValue("ColumnWidthState", 0));
            if (stateWidth > 0)
                PoliciesList.Columns[1].Width = stateWidth;
            int commentWidth = Conversions.ToInteger(Configuration.GetValue("ColumnWidthComment", 0));
            if (commentWidth > 0)
                PoliciesList.Columns[2].Width = commentWidth;
            int idWidth = Conversions.ToInteger(Configuration.GetValue("ColumnWidthId", 0));
            if (idWidth > 0)
                PoliciesList.Columns[3].Width = idWidth;
        }
        private void SavePaneLayout()
        {
            Configuration.SetValue("TreePaneWidth", SplitContainer.SplitterDistance);
            Configuration.SetValue("DescriptionPaneWidth", DescriptionSplitContainer.SplitterDistance);
            Configuration.SetValue("ColumnWidthState", PoliciesList.Columns[1].Width);
            Configuration.SetValue("ColumnWidthComment", PoliciesList.Columns[2].Width);
            Configuration.SetValue("ColumnWidthId", PoliciesList.Columns[3].Width);
        }
        private void Main_FormClosing(object sender, FormClosingEventArgs e)
        {
            SaveWindowBounds();
            SavePaneLayout();
            if (!_isDirty)
                return;
            var result = MsgBoxCompat.Show("There are unsaved changes. Save before closing?", MessageBoxButtons.YesNoCancel, MessageBoxIcon.Question);
            if (result == DialogResult.Cancel)
            {
                e.Cancel = true;
                _pendingRestartForColorMode = false; // Abandoned close attempt - don't relaunch on some later, unrelated close
                return;
            }
            if (result == DialogResult.Yes)
            {
                SavePoliciesToolStripMenuItem_Click(sender, e);
                if (_isDirty)
                {
                    e.Cancel = true; // Save failed (error already shown); don't close with unsaved changes
                    _pendingRestartForColorMode = false;
                }
            }
        }
        private void Main_Closed(object sender, EventArgs e)
        {
            ClosePolicySources(); // Make sure everything is cleaned up before quitting
            if (_pendingRestartForColorMode)
                Process.Start(Application.ExecutablePath);
        }
        private void PoliciesList_KeyDown(object sender, KeyEventArgs e)
        {
            // Activate a right pane item if the user presses Enter on it
            if (e.KeyCode == Keys.Enter & PoliciesList.SelectedItems.Count > 0)
                PoliciesList_DoubleClick(sender, e);
        }
        private void FilterOptionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Show the Filter Options dialog and refresh if the filter changes
            if (My.MyProject.Forms.FilterOptions.PresentDialog(CurrentFilter, AdmxWorkspace) == DialogResult.OK)
            {
                CurrentFilter = My.MyProject.Forms.FilterOptions.CurrentFilter;
                ViewFilteredOnly = true;
                OnlyFilteredObjectsToolStripMenuItem.Checked = true;
                MoveToVisibleCategoryAndReload();
            }
        }
        private void OnlyFilteredObjectsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Toggle whether the filter is used
            ViewFilteredOnly = !ViewFilteredOnly;
            OnlyFilteredObjectsToolStripMenuItem.Checked = ViewFilteredOnly;
            MoveToVisibleCategoryAndReload();
        }
        private void ImportSemanticPolicyToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Open the SPOL import dialog and apply the data
            if (My.MyProject.Forms.ImportSpol.ShowDialog() == DialogResult.OK)
            {
                var spol = My.MyProject.Forms.ImportSpol.Spol;
                int fails = spol.ApplyAll(AdmxWorkspace, UserPolicySource, CompPolicySource, UserComments, CompComments);
                _isDirty = true;
                MoveToVisibleCategoryAndReload();
                if (fails == 0)
                {
                    MsgBoxCompat.Show("Semantic Policy successfully applied.", MessageBoxButtons.OK, MessageBoxIcon.Information);
                }
                else
                {
                    MsgBoxCompat.Show(fails + " out of " + spol.Policies.Count + " could not be applied.", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                }
            }
        }
        private void ImportPOLToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Open a POL file and write it to a policy source
            using (var ofd = new OpenFileDialog())
            {
                ofd.Filter = "POL files|*.pol";
                if (ofd.ShowDialog() == DialogResult.OK)
                {
                    PolFile pol = null;
                    try
                    {
                        pol = PolFile.Load(ofd.FileName);
                    }
                    catch (Exception ex)
                    {
                        MsgBoxCompat.Show("The POL file could not be loaded.", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                        return;
                    }
                    if (My.MyProject.Forms.OpenSection.PresentDialog(true, true) == DialogResult.OK)
                    {
                        var section = My.MyProject.Forms.OpenSection.SelectedSection == AdmxPolicySection.User ? UserPolicySource : CompPolicySource;
                        pol.Apply(section);
                        _isDirty = true;
                        MoveToVisibleCategoryAndReload();
                        MsgBoxCompat.Show("POL import successful.", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                }
            }
        }
        private void ExportPOLToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Create a POL file from a current policy source
            using (var sfd = new SaveFileDialog())
            {
                sfd.Filter = "POL files|*.pol";
                if (sfd.ShowDialog() == DialogResult.OK && My.MyProject.Forms.OpenSection.PresentDialog(true, true) == DialogResult.OK)
                {
                    var section = My.MyProject.Forms.OpenSection.SelectedSection == AdmxPolicySection.Machine ? CompPolicySource : UserPolicySource;
                    try
                    {
                        GetOrCreatePolFromPolicySource(section).Save(sfd.FileName);
                        MsgBoxCompat.Show("POL exported successfully.", MessageBoxButtons.OK, MessageBoxIcon.Information);
                    }
                    catch (Exception ex)
                    {
                        MsgBoxCompat.Show("The POL file could not be saved.", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                    }
                }
            }
        }
        private void AcquireADMXFilesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            // Show the Acquire ADMX Files dialog and load the new ADMX files
            if (My.MyProject.Forms.DownloadAdmx.ShowDialog() == DialogResult.OK)
            {
                if (!string.IsNullOrEmpty(My.MyProject.Forms.DownloadAdmx.NewPolicySourceFolder))
                {
                    ClearAdmxWorkspace();
                    DisplayAdmxLoadErrorReport(AdmxWorkspace.LoadFolder(My.MyProject.Forms.DownloadAdmx.NewPolicySourceFolder, GetPreferredLanguageCode()));
                    Configuration.SetValue("AdmxSource", My.MyProject.Forms.DownloadAdmx.NewPolicySourceFolder);
                    PopulateAdmxUi();
                }
            }
        }
        private void LoadedADMXFilesToolStripMenuItem_Click(object sender, EventArgs e)
        {
            My.MyProject.Forms.LoadedAdmx.PresentDialog(AdmxWorkspace);
        }
        private void AllSupportDefinitionsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            My.MyProject.Forms.LoadedSupportDefinitions.PresentDialog(AdmxWorkspace);
        }
        private void AllProductsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            My.MyProject.Forms.LoadedProducts.PresentDialog(AdmxWorkspace);
        }
        private void EditRawPOLToolStripMenuItem_Click(object sender, EventArgs e)
        {
            bool userIsPol = UserPolicySource is PolFile;
            bool compIsPol = CompPolicySource is PolFile;
            if (!(userIsPol | compIsPol))
            {
                MsgBoxCompat.Show("Neither loaded source is backed by a POL file.", MessageBoxButtons.OK, MessageBoxIcon.Exclamation);
                return;
            }
            if (Conversions.ToInteger(Configuration.GetValue("EditPolDangerAcknowledged", 0)) == 0)
            {
                if (MsgBoxCompat.Show("Caution! This tool is for very advanced users. Improper modifications may result in inconsistencies in policies' states." + Constants.vbCrLf + Constants.vbCrLf + "Changes operate directly on the policy source, though they will not be committed to disk until you save. Are you sure you want to continue?", MessageBoxButtons.YesNo, MessageBoxIcon.Exclamation) == DialogResult.No)
                    return;
                Configuration.SetValue("EditPolDangerAcknowledged", 1);
            }
            if (My.MyProject.Forms.OpenSection.PresentDialog(userIsPol, compIsPol) == DialogResult.OK)
            {
                My.MyProject.Forms.EditPol.PresentDialog(PolicyIcons, (PolFile)(My.MyProject.Forms.OpenSection.SelectedSection == AdmxPolicySection.Machine ? CompPolicySource : UserPolicySource), My.MyProject.Forms.OpenSection.SelectedSection == AdmxPolicySection.User);
                // EditPol mutates the PolFile in place while open, regardless of how its window is closed
                _isDirty = true;
            }
            MoveToVisibleCategoryAndReload();
        }
        private void ExportREGToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (My.MyProject.Forms.OpenSection.PresentDialog(true, true) == DialogResult.OK)
            {
                var source = My.MyProject.Forms.OpenSection.SelectedSection == AdmxPolicySection.Machine ? CompPolicySource : UserPolicySource;
                My.MyProject.Forms.ExportReg.PresentDialog("", GetOrCreatePolFromPolicySource(source), My.MyProject.Forms.OpenSection.SelectedSection == AdmxPolicySection.User);
            }
        }
        private void ImportREGToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (My.MyProject.Forms.OpenSection.PresentDialog(true, true) == DialogResult.OK)
            {
                var source = My.MyProject.Forms.OpenSection.SelectedSection == AdmxPolicySection.Machine ? CompPolicySource : UserPolicySource;
                if (My.MyProject.Forms.ImportReg.PresentDialog(source) == DialogResult.OK)
                {
                    _isDirty = true;
                    MoveToVisibleCategoryAndReload();
                }
            }
        }
        private void SetADMLLanguageToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (My.MyProject.Forms.LanguageOptions.PresentDialog(GetPreferredLanguageCode()) == DialogResult.OK)
            {
                Configuration.SetValue("LanguageCode", My.MyProject.Forms.LanguageOptions.NewLanguage);
                if (MsgBoxCompat.Show("Language changes will take effect when ADML files are next loaded. Would you like to reload the workspace now?", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
                {
                    ClearAdmxWorkspace();
                    OpenLastAdmxSource();
                    PopulateAdmxUi();
                }
            }
        }
        private void SetColorModeMenuChecks(string ColorMode)
        {
            LightToolStripMenuItem.Checked = ColorMode == "Light";
            DarkToolStripMenuItem.Checked = ColorMode == "Dark";
            SystemToolStripMenuItem.Checked = ColorMode == "System";
        }
        private void ApplyColorModeChoice(string ColorMode)
        {
            Configuration.SetValue("ColorMode", ColorMode);
            SetColorModeMenuChecks(ColorMode);
            // SetColorMode is a startup-only, one-time API - can't re-theme controls already created,
            // so applying a new choice needs a real restart, not just a message
            if (MsgBoxCompat.Show("Restart Policy Plus now to apply the new color mode?", MessageBoxButtons.YesNo, MessageBoxIcon.Question) == DialogResult.Yes)
            {
                _pendingRestartForColorMode = true;
                Close(); // Goes through Main_FormClosing/Main_Closed, respecting the unsaved-changes prompt
            }
        }
        private void LightToolStripMenuItem_Click(object sender, EventArgs e) => ApplyColorModeChoice("Light");
        private void DarkToolStripMenuItem_Click(object sender, EventArgs e) => ApplyColorModeChoice("Dark");
        private void SystemToolStripMenuItem_Click(object sender, EventArgs e) => ApplyColorModeChoice("System");
        private void PolicyObjectContext_Opening(object sender, CancelEventArgs e)
        {
            // When the right-click menu is opened
            bool showingForCategory;
            if (ReferenceEquals(PolicyObjectContext.SourceControl, CategoriesTree))
            {
                showingForCategory = true;
                PolicyObjectContext.Tag = CategoriesTree.SelectedNode.Tag;
            }
            else if (PoliciesList.SelectedItems.Count > 0) // Shown from the main view
            {
                var selEntryTag = PoliciesList.SelectedItems[0].Tag;
                showingForCategory = selEntryTag is PolicyPlusCategory;
                PolicyObjectContext.Tag = selEntryTag;
            }
            else
            {
                e.Cancel = true;
                return;
            }
            // Items are tagged in the designer for the objects they apply to
            foreach (var item in PolicyObjectContext.Items.OfType<ToolStripMenuItem>())
            {
                bool ok = true;
                if (Conversions.ToString(item.Tag) == "P" & showingForCategory)
                    ok = false;
                if (Conversions.ToString(item.Tag) == "C" & !showingForCategory)
                    ok = false;
                item.Visible = ok;
            }
            if (!showingForCategory && PolicyObjectContext.Tag is PolicyPlusPolicy policy)
            {
                CmeFavoriteToggle.Text = FavoriteIds.Contains(policy.UniqueID) ? "Remove from Favorites" : "Add to Favorites";
            }
            // The copy-to-clipboard group is policy-only; hide its separator along with those items
            CmeCopySeparator.Visible = !showingForCategory;
        }
        private void PolicyObjectContext_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {
            // When the user clicks an item in the right-click menu
            var polObject = PolicyObjectContext.Tag; // The current policy object is in the Tag field
            if (ReferenceEquals(e.ClickedItem, CmeCatOpen))
            {
                CurrentCategory = (PolicyPlusCategory)polObject;
                UpdateCategoryListing();
            }
            else if (ReferenceEquals(e.ClickedItem, CmePolEdit))
            {
                ShowSettingEditor((PolicyPlusPolicy)polObject, ViewPolicyTypes);
            }
            else if (ReferenceEquals(e.ClickedItem, CmeFavoriteToggle))
            {
                string id = ((PolicyPlusPolicy)polObject).UniqueID;
                if (!FavoriteIds.Remove(id))
                    FavoriteIds.Add(id);
                Configuration.SetValue("Favorites", FavoriteIds.ToArray());
                if (ReferenceEquals(CategoriesTree.SelectedNode, FavoritesNode))
                    UpdateCategoryListing();
            }
            else if (ReferenceEquals(e.ClickedItem, CmeAllDetails))
            {
                if (polObject is PolicyPlusCategory)
                {
                    My.MyProject.Forms.DetailCategory.PresentDialog((PolicyPlusCategory)polObject);
                }
                else
                {
                    My.MyProject.Forms.DetailPolicy.PresentDialog((PolicyPlusPolicy)polObject);
                }
            }
            else if (ReferenceEquals(e.ClickedItem, CmePolInspectElements))
            {
                My.MyProject.Forms.InspectPolicyElements.PresentDialog((PolicyPlusPolicy)polObject, PolicyIcons, AdmxWorkspace);
            }
            else if (ReferenceEquals(e.ClickedItem, CmePolSpolFragment))
            {
                My.MyProject.Forms.InspectSpolFragment.PresentDialog((PolicyPlusPolicy)polObject, AdmxWorkspace, CompPolicySource, UserPolicySource, CompComments, UserComments);
            }
            else if (ReferenceEquals(e.ClickedItem, CmeCopyId))
            {
                Clipboard.SetText(((PolicyPlusPolicy)polObject).UniqueID);
            }
            else if (ReferenceEquals(e.ClickedItem, CmeCopyName))
            {
                Clipboard.SetText(((PolicyPlusPolicy)polObject).DisplayName);
            }
            else if (ReferenceEquals(e.ClickedItem, CmeCopyRegPath))
            {
                var rawPolicy = ((PolicyPlusPolicy)polObject).RawPolicy;
                string root = rawPolicy.Section == AdmxPolicySection.User ? @"HKEY_CURRENT_USER\" : @"HKEY_LOCAL_MACHINE\";
                string path = root + rawPolicy.RegistryKey;
                if (!string.IsNullOrEmpty(rawPolicy.RegistryValue))
                    path += @"\" + rawPolicy.RegistryValue;
                Clipboard.SetText(path);
            }
        }
        private void CategoriesTree_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            // Right-clicking doesn't actually select the node by default
            if (e.Button == MouseButtons.Right)
                CategoriesTree.SelectedNode = e.Node;
        }
        public static string PrettifyDescription(string Description)
        {
            // Remove extra indentation from paragraphs
            var sb = new StringBuilder();
            foreach (var line in Description.Split(Constants.vbCrLf))
                sb.AppendLine(line.Trim());
            return sb.ToString().TrimEnd();
        }

        // A ToolStripTextBox that claims whatever space is left over in its owning ToolStrip
        // (after every other item's own width), clamped between a minimum and a preferred width -
        // the standard WinForms pattern for a resizable toolbar search box.
        internal class ToolStripSpringTextBox : ToolStripTextBox
        {
            [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
            public int MinimumWidth { get; set; } = 150;
            [System.ComponentModel.DesignerSerializationVisibility(System.ComponentModel.DesignerSerializationVisibility.Hidden)]
            public int PreferredWidth { get; set; } = 400;

            public override Size GetPreferredSize(Size constrainingSize)
            {
                int preferredHeight = base.GetPreferredSize(constrainingSize).Height;
                if (Owner is null)
                    return new Size(MinimumWidth, preferredHeight);
                int othersWidth = 0;
                foreach (ToolStripItem item in Owner.Items)
                {
                    if (!ReferenceEquals(item, this) && item.Visible)
                        othersWidth += item.Width + item.Margin.Horizontal;
                }
                int available = Owner.DisplayRectangle.Width - othersWidth;
                int width = Math.Max(MinimumWidth, Math.Min(available, PreferredWidth));
                return new Size(width, preferredHeight);
            }
        }
    }
}