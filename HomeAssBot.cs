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

namespace HomeAssBot
{
    internal class HomeAssBot
    {
        private string _token;
        private string _applicationPath;
        private string _imagePath = "images\\";

        // Discord
        private DiscordClient _discordClient;

        // Commands
        private CommandParser _commandParser;

        // http
        HttpClient _httpClient;

        public void Initialize()
        {
            _applicationPath = AppContext.BaseDirectory + @"..\..\..\";
            this._token = System.IO.File.ReadAllText(_applicationPath + "token.txt");
            this._commandParser = new CommandParser();
            this._httpClient = new HttpClient();
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
        };
        private async Task ProcessCommand(DiscordClient sender, MessageCreateEventArgs e)
        {
            var author = e.Author;
            var message = e.Message;
            var message_content = message.Content;

            dynamic parsed_command = _commandParser.Parse(message_content);
            if (parsed_command == null)
                return;


            var command = parsed_command["command"];
            if (command == null)
            {
                await e.Message.RespondAsync("No such command");
                return;
            }



            string response = "default response";
            if (command == "ai")
            {
                var prompt = parsed_command["!ai"];
                Console.WriteLine(prompt);

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

                Int64 cfg_seed = new Random(Guid.NewGuid().GetHashCode()).NextInt64();
                try
                {
                    cfg_seed = Int64.Parse(parsed_command["seed"]);
                }
                catch
                {
                }

                var cfg_negative = parsed_command["negative"];

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
                    //Console.WriteLine("INFO: " + infoparse);
                    var info_prompt = infoparse["prompt"].ToString().Replace(' ', '_');
                    //Console.WriteLine(info_prompt);
                    var info_seed = infoparse["seed"].ToString();
                    //Console.WriteLine(info_seed);
                    var info_steps = infoparse["steps"].ToString();
                    //Console.WriteLine(info_steps);

                    string file_path = _applicationPath + _imagePath + $"{info_prompt}{info_seed}_{info_steps}steps.png";
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
                        NegativePrompts = cfg_negative
                    };

                    using (var fs = new FileStream(ai_info.Full_Path, FileMode.Open, FileAccess.Read))
                    {
                        var msg = await new DiscordMessageBuilder()
                            .WithFiles(new Dictionary<string, Stream>() { { ai_info.Full_Path, fs } })
                            .SendAsync(message.Channel);
                        await msg.DeleteAsync();

                        var image_url = msg.Attachments.FirstOrDefault().Url;
                        msg = await new DiscordMessageBuilder()
                            .WithEmbed(new DiscordEmbedBuilder()
                            {
                                Title = ai_info.Prompt,
                                ImageUrl = image_url
                            }
                            .AddField("Seed", ai_info.Seed)
                            .AddField("Steps", ai_info.Steps)
                            .WithFooter("Negative Prompts: " + ai_info.NegativePrompts)
                            )
                            .SendAsync(message.Channel);
                    }
                }
            }
        }
    }
}
