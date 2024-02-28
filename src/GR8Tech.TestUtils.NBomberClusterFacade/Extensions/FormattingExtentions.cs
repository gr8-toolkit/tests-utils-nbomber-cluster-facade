using Newtonsoft.Json;

namespace GR8Tech.TestUtils.NBomberClusterFacade.Extensions;

public static class FormattingExtensions
{
    internal static  string ToReadableString(this object @object)
    {
        return JsonConvert.SerializeObject(@object, Formatting.Indented);
    }
}