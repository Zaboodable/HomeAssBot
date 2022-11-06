using DSharpPlus;
using DSharpPlus.EventArgs;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace HomeAssBot
{
    internal class HomeAssBot
    {
        private string _token;
        private string _applicationPath;

        // Discord
        private DiscordClient _discordClient;

        // Commands
        private CommandParser _commandParser;

        public void Initialize()
        {
            _applicationPath = AppContext.BaseDirectory + @"..\..\..\";
            this._token = System.IO.File.ReadAllText(_applicationPath + "token.txt");
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

            _commandParser = new CommandParser();
            _discordClient.MessageCreated += async (sender, e) =>
            {
                if (e.Author.IsBot)
                    return;
                if (e.Message.Content[0] == '!')
                {
                    await ProcessCommand(sender, e);
                }
            };


            await _discordClient.ConnectAsync();
            await Task.Delay(-1);
        }

        private async Task ProcessCommand(DiscordClient sender, MessageCreateEventArgs e)
        {
            var author = e.Author;
            var message = e.Message;
            var content = message.Content;

            dynamic parsed_command = _commandParser.Parse(content);
            if (parsed_command == null)
                return;

            await e.Message.RespondAsync(parsed_command.ToString());
        }


    }
}
