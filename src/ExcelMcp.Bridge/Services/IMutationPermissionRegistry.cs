using ExcelMcp.Core;

namespace ExcelMcp.Bridge.Services;

public interface IMutationPermissionRegistry
{
    MutationPermissionLease GrantWorkbook(string workbookPath, TimeSpan ttl, out bool refreshedExistingLease);
    MutationPermissionLease GrantSession(TimeSpan ttl, out bool refreshedExistingLease);
    bool RevokeWorkbook(string workbookPath);
    bool RevokeSession();
    MutationPermissionLookup Lookup(string workbookPath);
    MutationPermissionLookup LookupSession();
    MutationPermissionLease TouchWorkbook(string workbookPath);
    MutationPermissionLease TouchSession();
}
