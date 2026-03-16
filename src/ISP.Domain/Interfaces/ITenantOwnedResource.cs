namespace ISP.Domain.Interfaces
{
    /// <summary>
    /// Marks an entity as tenant-owned
    /// Enables Resource-Based Authorization via TenantOwnershipHandler
    /// </summary>
    public interface ITenantOwnedResource
    {
        /// <summary>
        /// The tenant this resource belongs to
        /// Null means SuperAdmin-only access
        /// </summary>
        int? TenantId { get; }
    }
}