using ExcelMcp.Core;

namespace ExcelMcp.Bridge.Services;

public interface IAttachedMutationApprovalRegistry
{
    AttachedMutationApprovalLease Grant(string workbookPath, TimeSpan ttl, out bool refreshedExistingLease);
    bool Revoke(string workbookPath);
    AttachedMutationApprovalLookup Lookup(string workbookPath);
    AttachedMutationApprovalLease Touch(string workbookPath);
}
