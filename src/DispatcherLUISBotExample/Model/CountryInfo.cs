using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace DispatcherLUISBotExample.Model
{
    public class CountryInfo
    {
        [JsonProperty(PropertyName = "name")]
        public string Name { get; set; }
        [JsonProperty(PropertyName = "alpha-2")]
        public string Alpha2 { get; set; }
        [JsonProperty(PropertyName = "country-code")]
        public string Code { get; set; }
    }
}
