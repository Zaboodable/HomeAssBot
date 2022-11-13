using DSharpPlus;
using DSharpPlus.EventArgs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using DSharpPlus.Entities;
using System.Drawing;
using System.Runtime;
using System.IO;

namespace HomeAssBot
{
    internal class HomeAssBot
    {
        private string _token;
        private string _applicationPath;
        private string _imagePath = "images/";
        private char[] _filename_bad_chars;

        // Discord
        private DiscordClient _discordClient;

        // Commands
        private CommandParser _commandParser;

        // AI Stable Diffusion
        private List<string> _ai_samplers = new List<string>();

        // http
        HttpClient _httpClient;

        public void Initialize()
        {
            _applicationPath = AppContext.BaseDirectory + @"../../../";
            this._token = System.IO.File.ReadAllText(_applicationPath + "token.txt");
            this._commandParser = new CommandParser();
            this._httpClient = new HttpClient();
            this._filename_bad_chars = Path.GetInvalidFileNameChars();
        }

        private void Initialize_AI_Values()
        {
            var httpWebRequest = (HttpWebRequest)WebRequest.Create("http://127.0.0.1:7860/sdapi/v1/samplers");
            httpWebRequest.ContentType = "application/json";
            httpWebRequest.Method = "GET";
            var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
            using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
            {
                var result = streamReader.ReadToEnd();
                JArray parsed_response = JArray.Parse(result);
                foreach (JObject item in parsed_response)
                {
                    JToken name;
                    if (item.TryGetValue("name", out name))
                    {
                        _ai_samplers.Add(name.Value<string>());
                    }
                }

            }
        }

        public async Task Run()
        {
            var config = new DiscordConfiguration()
            {
                Token = this._token,
                TokenType = TokenType.Bot,
                Intents = DiscordIntents.All
            };
            _discordClient = new DiscordClient(config);


            Initialize_AI_Values();


            _discordClient.MessageCreated += (s, e) =>
            {
                if (e.Author.IsBot)
                    return Task.CompletedTask;
                if (e.Message.Content[0] == '!')
                {
                    _ = Task.Run(async () =>
                    {
                        await ProcessCommand(s, e);
                    });
                }
                return Task.CompletedTask;
            };


            await _discordClient.ConnectAsync();
            await Task.Delay(-1);
        }
        struct AI_Info
        {
            public string Full_Path;
            public string Prompt;
            public string Seed;
            public string Steps;
            public string NegativePrompts;
            public string Sampler;
            public string Scale;
        };
        private async Task ProcessCommand(DiscordClient sender, MessageCreateEventArgs e)
        {
            var author = e.Author;
            var message = e.Message;
            var message_content = message.Content;

            dynamic parsed_command = _commandParser.Parse(message_content);
            if (parsed_command == null)
                return;

            string response = "default response";

            var command = parsed_command["command"];
            if (command == null)
            {
                await e.Message.RespondAsync("No such command");
                return;
            }

            if (command == "ai")
            {
                var prompt = parsed_command["!ai"];
                Console.WriteLine(message_content);

                if (parsed_command["help"] != null || parsed_command["h"] != null)
                {
                    await e.Message.RespondAsync($"soon come help i add when back.\n!ai <prompt> -steps 1-100 -sampler <sampler> -seed <seed> -scale <scale>");
                    return;
                }
                if (parsed_command["samplers"] != null)
                {
                    string samplerList = "";
                    foreach (var sampler in _ai_samplers)
                    {
                        samplerList += "[\"" + sampler + "\"], ";
                    }
                    samplerList.TrimEnd(',');
                    await e.Message.RespondAsync($"Here is a list of available samplers, different samplers will generate a different image with the same seed:\n{samplerList}\nuse with -sampler.\nexample: !ai chicken -sampler Euler a");
                    return;
                }

                // STEPS -------------------------------------------------------
                var cfg_steps = 20;
                try
                {
                    cfg_steps = int.Parse(parsed_command["steps"]);
                }
                catch
                {
                }
                if (cfg_steps < 1)
                    cfg_steps = 1;
                if (cfg_steps > 100)
                    cfg_steps = 100;
                
                // SEED --------------------------------------------------------
                Int64 cfg_seed = new Random(Guid.NewGuid().GetHashCode()).NextInt64();
                try
                {
                    cfg_seed = Int64.Parse(parsed_command["seed"]);
                }
                catch
                {
                }

                // NEGATIVE PROMPT --------------------------------------------------------
                var cfg_negative = parsed_command["negative"];

                // SAMPLER --------------------------------------------------------
                var cfg_sampler = "Euler";
                var parsed_sampler = parsed_command["sampler"];
                if (parsed_sampler != null)
                {
                    parsed_sampler = parsed_sampler.TrimEnd();
                    if (_ai_samplers.Contains(parsed_sampler))
                        cfg_sampler = parsed_sampler;
                    else
                    {
                        string samplerList = "";
                        foreach (var sampler in _ai_samplers)
                        {
                            samplerList += sampler + ", ";
                        }
                        samplerList.TrimEnd(',');
                        await e.Message.RespondAsync($"\"{parsed_sampler}\" is not in the list of installed samplers\n{samplerList}");
                    }
                }

                // SCALE --------------------------------------------------------
                string parsed_scale = parsed_command["scale"];
                float cfg_scale = 7.0f;
                try
                {
                    cfg_scale = Single.Parse(parsed_scale);
                }
                catch
                {
                }


                string url = "http://127.0.0.1:7860/sdapi/v1/txt2img";

                var httpWebRequest = (HttpWebRequest)WebRequest.Create(url);
                httpWebRequest.ContentType = "application/json";
                httpWebRequest.Method = "POST";

                using (var streamWriter = new StreamWriter(httpWebRequest.GetRequestStream()))
                {
                    string json = "";
                    json += "{";
                    json += $"\"prompt\": \"{prompt}\",";
                    json += $"\"steps\": \"{cfg_steps.ToString()}\",";
                    json += $"\"restore_faces\": true,";
                    json += $"\"sampler_index\": \"{cfg_sampler}\",";
                    json += $"\"cfg_scale\": \"{cfg_scale}\",";
                    if (cfg_negative != null)
                        json += $"\"negative_prompt\": \"{cfg_negative}\",";

                    json += $"\"seed\": {cfg_seed.ToString()}";
                    json += "}";

                    streamWriter.Write(json);
                }

                var httpResponse = (HttpWebResponse)httpWebRequest.GetResponse();
                using (var streamReader = new StreamReader(httpResponse.GetResponseStream()))
                {
                    var result = streamReader.ReadToEnd();
                    JObject parsed_response = JObject.Parse(result);

                    // Image -------------------------------------------
                    JToken image_b64 = parsed_response["images"][0];
                    var img = image_b64.ToString();
                    var bytes = System.Convert.FromBase64String(img);

                    // Metadata ----------------------------------------
                    var image_info = parsed_response["info"].ToString();
                    
                    JObject infoparse = JObject.Parse(image_info);
                    var info_prompt = infoparse["prompt"].ToString().Replace(' ', '_');
                    var info_seed = infoparse["seed"].ToString();
                    var info_steps = infoparse["steps"].ToString();
                    var info_sampler = infoparse["sampler"].ToString();
                    var info_scale = infoparse["cfg_scale"].ToString();

                    string file_name = $"{info_prompt}{info_seed}_{info_steps}steps.png";
                    foreach (char bc in _filename_bad_chars)
                    {
                        file_name = file_name.Replace(bc, '_');
                    }

                    string file_path = _applicationPath + _imagePath + file_name;
                    if (Directory.Exists(_applicationPath + _imagePath) == false)
                    {
                        Directory.CreateDirectory(_applicationPath + _imagePath);
                    }
                    var a = new DiscordMessageBuilder();

                    string full_path = Path.GetFullPath(file_path);

                    using (FileStream fs = File.Create(file_path))
                    {
                        fs.Write(bytes, 0, bytes.Length);
                    }

                    var ai_info = new AI_Info()
                    {
                        Full_Path = full_path,
                        Prompt = prompt,
                        Seed = info_seed,
                        Steps = info_steps,
                        Sampler = info_sampler,
                        Scale = info_scale,
                        NegativePrompts = cfg_negative
                    };

                    using (var fs = new FileStream(ai_info.Full_Path, FileMode.Open, FileAccess.Read))
                    {
                        // Fixed finally
                        // file attached to message
                        // embed image url references the attached image name eg attachment://image.png
                        var msg = new DiscordMessageBuilder()
                        .WithFiles(new Dictionary<string, Stream>() { { ai_info.Full_Path, fs } })
                        .WithEmbed(new DiscordEmbedBuilder()
                            .WithTitle(ai_info.Prompt)
                            .WithImageUrl(Formatter.AttachedImageUrl(file_name))
                            .AddField("Seed", ai_info.Seed)
                            .AddField("Steps", ai_info.Steps)
                            .AddField("Sampler", ai_info.Sampler)
                            .AddField("CFG Scale", ai_info.Scale)
                            .WithFooter("Negative Prompts: " + ai_info.NegativePrompts)
                        .Build());
                        await e.Message.RespondAsync(msg);
                    }
                }
            }
        }
    }
}
