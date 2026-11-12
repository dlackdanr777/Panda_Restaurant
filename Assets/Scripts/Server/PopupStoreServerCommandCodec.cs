using System;
using System.IO;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace PandaRestaurant.PopupStoreServer
{
    /// <summary>The actual Function request decoder, shared with offline contract tests.
    /// Decoding never supplies authentication, score, grade, wallet or lifecycle evidence.
    /// Command semantics and server-owned policy remain the authority's responsibility.</summary>
    public static class PopupStoreServerCommandCodec
    {
        public const int MaximumCharacters = 65536;

        public static bool TryDecode(string json, out Command command, out Response failure)
        {
            command = null;
            failure = null;
            string requestId = null;
            if (json == null || json.Length > MaximumCharacters)
            {
                failure = Reject(null, json == null ? "invalid_request" : "request_too_large");
                return false;
            }
            try
            {
                JObject input;
                // An identifier that resembles an ISO timestamp is still a JSON string.
                // Do not let the JSON reader silently turn it into a Date token.
                using (var text = new StringReader(json))
                using (var reader = new JsonTextReader(text) { DateParseHandling = DateParseHandling.None, MaxDepth = 64 })
                {
                    input = JObject.Load(reader, new JsonLoadSettings
                        { DuplicatePropertyNameHandling = DuplicatePropertyNameHandling.Error });
                    if (reader.Read()) throw new JsonReaderException("Unexpected content after command.");
                }
                if (input["RequestId"]?.Type == JTokenType.String) requestId = (string)input["RequestId"];
                if (!HasExactShape(input))
                {
                    failure = Reject(requestId, "invalid_request_shape");
                    return false;
                }
                if ((string)input["Protocol"] != PopupStoreServerAuthority.Protocol)
                {
                    failure = Reject(requestId, "protocol_mismatch");
                    return false;
                }
                command = input.ToObject<Command>(JsonSerializer.Create(new JsonSerializerSettings
                    { MissingMemberHandling = MissingMemberHandling.Error, TypeNameHandling = TypeNameHandling.None }));
                return true;
            }
            catch (Exception)
            {
                failure = Reject(requestId, "invalid_request");
                return false;
            }
        }

        private static Response Reject(string requestId, string error)
        { return PopupStoreServerAuthority.CreateHostFailureResponse(requestId, Status.Invalid, error); }

        private static bool HasExactShape(JObject input)
        {
            if (input["Protocol"]?.Type != JTokenType.String || input["RequestId"]?.Type != JTokenType.String
                || input["Operation"]?.Type != JTokenType.Integer) return false;
            long operation = (long)input["Operation"];
            if (operation < (int)Operation.Access || operation > (int)Operation.Abandon) return false;
            foreach (var property in input.Properties())
            {
                switch (property.Name)
                {
                    case "Operation": break;
                    case "Protocol": case "RequestId": case "LookupRequestId":
                    case "BusinessSessionId": case "ExpectedPolicyVersion": case "OrderId": case "ReceiptId":
                        if (property.Value.Type != JTokenType.String && property.Value.Type != JTokenType.Null) return false;
                        break;
                    case "Materials":
                        if (property.Value.Type == JTokenType.Null) break;
                        var materials = property.Value as JArray;
                        if (materials == null || materials.Count > 64) return false;
                        foreach (var material in materials) if (material.Type != JTokenType.String) return false;
                        break;
                    default: return false;
                }
            }
            return true;
        }
    }
}
