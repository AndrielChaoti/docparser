using System.Collections.Generic;
using Newtonsoft.Json;

namespace SBDocParse {
    class SublimeCompletions {

        public string source { get; set; }
        public List<APICompletion> completions { get; set; }

        public class APICompletion {

            [JsonProperty(Required = Required.Always)]
            public string trigger { get; set; }
            public string contents { get; set; }

            public void AddHint(string hint) {
                trigger += "\t" + hint;
            }
        }
    }
}
