using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static BLTOverlay.BLTProtocol;

namespace BLTOverlay
{
    public class BLTProtocol
    {
        public class BltMessage
        {
            public int V { get; set; } = 1;
            public string Id { get; set; }
            public string Kind { get; set; }
            public string Source { get; set; }
            public string Target { get; set; }
            public BltUser User { get; set; }
            public long Ts { get; set; }
            public object Data { get; set; }
        }

        public class BltUser
        {
            public string Id { get; set; }
            public string Name { get; set; }
        }

        public class CommandData
        {
            public string Name { get; set; }
            public Dictionary<string, object> Args { get; set; }
        }
    }

    public static class OverlayCommandTranslator
    {
        public static string ToLegacyString(CommandData cmd)
        {
            if (cmd == null || string.IsNullOrEmpty(cmd.Name))
                return null;

            switch (cmd.Name)
            {
                case "party.army.join":
                    return $"party army join {cmd.Args["index"]}";

                case "formation.set":
                    return $"formation {cmd.Args["id"]}";

                case "proposal.accept":
                    return $"acceptProposal {cmd.Args["type"]} {cmd.Args["from"]}";

                case "proposal.reject":
                    return $"rejectProposal {cmd.Args["type"]} {cmd.Args["from"]}";
            }

            // fallback (CRITICAL for compatibility)
            if (cmd.Args != null && cmd.Args.Count > 0)
            {
                return cmd.Name.Replace('.', ' ') + " " +
                       string.Join(" ", cmd.Args.Values);
            }

            return cmd.Name.Replace('.', ' ');
        }
    }
}
