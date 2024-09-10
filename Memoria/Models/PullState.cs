using Newtonsoft.Json.Converters;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace Memoria.Models
{
    public enum PullState
    {
        Unknown,
        Canceled,
        Wiped,
        Cleared,
        ManullayStoped
    }
}
