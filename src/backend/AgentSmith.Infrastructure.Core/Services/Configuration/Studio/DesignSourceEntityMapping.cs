using AgentSmith.Contracts.Models.ConfigStudio;
using AgentSmith.Contracts.Models.Configuration;
using AgentSmith.Domain.Exceptions;

namespace AgentSmith.Infrastructure.Core.Services.Configuration.Studio;

/// <summary>
/// 2026-10-01-7f7aa: both directions between a raw <c>design_sources:</c> entry and its studio
/// entity, in a file of their own — <see cref="ConfigCatalogMapper"/> and
/// <see cref="RawConfigPatch"/> both sit on the file-length baseline. The vendor travels as its
/// YAML word ('figma'); a word naming no vendor is refused rather than defaulted.
/// </summary>
internal static class DesignSourceEntityMapping
{
    public static DesignSourceEntity ToEntity(string id, RawDesignSourceEntry raw) =>
        new(id, VendorName(raw.Vendor), raw.Auth, raw.DisplayName);

    public static RawDesignSourceEntry Apply(DesignSourceEntity entity, RawDesignSourceEntry? existing)
    {
        var source = existing ?? new RawDesignSourceEntry();
        source.Vendor = ParseVendor(entity.Vendor);
        source.Auth = entity.AuthSecret;
        source.DisplayName = string.IsNullOrWhiteSpace(entity.DisplayName) ? null : entity.DisplayName;
        return source;
    }

    private static string VendorName(DesignSourceVendor vendor) => vendor.ToString().ToLowerInvariant();

    private static DesignSourceVendor ParseVendor(string vendor)
    {
        var word = vendor.Trim().ToLowerInvariant();
        foreach (var known in Enum.GetValues<DesignSourceVendor>())
            if (VendorName(known) == word) return known;
        throw new ConfigurationException(
            $"Unknown design source vendor '{vendor}'. Known: "
            + string.Join(", ", Enum.GetValues<DesignSourceVendor>().Select(VendorName)) + ".");
    }
}
