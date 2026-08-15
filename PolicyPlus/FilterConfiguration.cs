using System.Collections.Generic;

namespace PolicyPlus
{
    public enum FilterPolicyState
    {
        Configured,
        NotConfigured,
        Enabled,
        Disabled
    }

    public class FilterConfiguration
    {
        public bool? ManagedPolicy;
        public FilterPolicyState? PolicyState;
        public bool? Commented;
        public List<PolicyPlusProduct> AllowedProducts;
        public bool AlwaysMatchAny;
        public bool MatchBlankSupport;
    }
}
