using System;
using System.Collections.Generic;
using Newtonsoft.Json;

namespace XamlNavigator.Models
{
    public class PageRecordModel
    {
        private string _valuesJson = "{}";
        private Dictionary<string, object> _values;

        public long Id { get; set; }
        public long PageId { get; set; }

        public string ValuesJson
        {
            get { return _valuesJson; }
            set
            {
                _valuesJson = value;
                _values = null;
            }
        }

        public DateTime CreatedAt { get; set; }

        [JsonIgnore]
        public object this[string field]
        {
            get
            {
                if (_values == null)
                {
                    if (string.IsNullOrEmpty(ValuesJson)) _values = new Dictionary<string, object>();
                    else _values = JsonConvert.DeserializeObject<Dictionary<string, object>>(ValuesJson) ?? new Dictionary<string, object>();
                }
                object value;
                return _values.TryGetValue(field, out value) ? value : null;
            }
        }
    }
}
