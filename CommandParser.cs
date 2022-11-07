using System.Dynamic;
using System.Xml.Linq;

namespace HomeAssBot
{
    internal class CommandParser
    {
        private string prefix = "!";
        private string arg_prefix = "-";
        public CommandParser()
        {
        }

        public ParsedCommand Parse(string text)
        {
            dynamic commandExpando = new ExpandoObject();
            ParsedCommand parsedCommand = new ParsedCommand(commandExpando);

            var split = text.Split(' ');

            // Extract the command from the text
            var command_split = split[0].ToLower();
            if (command_split.StartsWith(prefix) == false)
            {
                return null;
            }
            else
            {
                commandExpando.command = command_split.Replace(prefix, "");
            }

            // Split the string into its arguments
            // For each argument, add a new property to the expando
            var arg_split = text.Split(arg_prefix);
            foreach (var arg in arg_split)
            {
                // the first word following a prefix should be the name of that argument
                // eg: -help -> help
                var arg_word_split = arg.Split(' ');
                var arg_name = arg_word_split[0];
                
                // check the content after the prefixed argument for the argument value(s)
                if (arg_word_split.Length > 1)
                {
                    var x = commandExpando as IDictionary<string, object>;
                    var arg_values = arg_word_split.Take(new Range(1, arg_word_split.Length)).ToArray();
                    if (arg_values.Count() == 1)
                    {
                        // if the argument has one word after it, add it directly
                        
                        x.Add(arg_name, arg_values[0]);
                    } 
                    else
                    {
                        // if the argument has multiple words after it, add them as a single string
                        string s = "";
                        for (int i = 0; i < arg_values.Length; i++)
                        {
                            s += arg_values[i];
                            if (i < arg_values.Length - 1)
                                s += ' ';
                        }
                        x.Add(arg_name, s);
                    }
                }
            }


            return parsedCommand;
        }
    }

    internal class ParsedCommand
    {
        private ExpandoObject _dynamicObject;
    
        public ParsedCommand(ExpandoObject expando)
        {
            this._dynamicObject = expando;
        }

        public object this[string key]
        {
            get
            {
                object? value;
                var dict = ((IDictionary<string, object>)_dynamicObject);
                if (dict.ContainsKey(key))
                {
                    value = dict[key];
                } else
                {
                    value = null;
                }
                return value;
            }
            set {  }
        }

        public override string ToString()
        {
            var dict = ((IDictionary<string, object>)_dynamicObject);
            var keys = dict.Keys;
            string s = "";

            foreach (var key in keys)
            {
                s += $"{key}: {dict[key]}\n";
            }
            return s;
        }

    }

}