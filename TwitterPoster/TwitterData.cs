using System;
using System.Collections.Generic;

namespace TwitterPoster
{
    class TwitterData
    {
        public List<Data> data { get; set; }
        public Meta meta { get; set; }
    }

    class Data
    {
        public DateTime created_at { get; set; }
        public string id { get; set; }
        public string text { get; set; }
    }
    class Meta
    {
        public string newest_id { get; set; }
        public string oldest_id { get; set; }
        public string result_count { get; set; }
        public string next_token { get; set; }
    }
}
