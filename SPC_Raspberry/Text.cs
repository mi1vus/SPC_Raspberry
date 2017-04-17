using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;

namespace ProjectSummer.Repository
{
    public class Text
    {
        public static string FormatXML(string xml)
        {
           // xml = xml.Replace("\r", "").Replace("\n", "");
            int tab = 0;
            string pattern = @"<(/?\w+[>|\s])";
            Regex regex = new Regex(pattern);
            var match = regex.Matches(xml);
            for (int z = 0; z < match.Count; z++)
            {
                if (!match[z].Groups[1].Value.StartsWith("/"))
                    xml = xml.Replace("<" + match[z].Groups[1].Value, "\r\n".PadRight(tab++ + 2, '\t') + "<" + match[z].Groups[1].Value);
                else
                {
                    var q = "\r\n".PadRight(--tab + 2, '\t');
                    if ("/" + match[z - 1].Groups[1].Value.Remove(match[z - 1].Groups[1].Value.Length - 1, 1)
                           == match[z].Groups[1].Value.Remove(match[z].Groups[1].Value.Length - 1, 1))
                        q = "";
                    xml = xml.Replace("<" + match[z].Groups[1].Value, q + "<" + match[z].Groups[1].Value);
                }
            }
            xml.TrimStart('\r','\n');
            return xml;
        }

    }
}
