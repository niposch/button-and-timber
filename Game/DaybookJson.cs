using System.Text.Json.Serialization;

namespace ButtonAndTimber;

[JsonSerializable(typeof(ShopState))]
internal sealed partial class DaybookJson : JsonSerializerContext
{
}
