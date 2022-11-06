namespace HomeAssBot
{
    internal class Program
    {
        static void Main(string[] args)
        {
            MainAsync().GetAwaiter().GetResult();
        }

        static async Task MainAsync()
        {
            HomeAssBot homeAssBot = new HomeAssBot();
            homeAssBot.Initialize();
            await homeAssBot.Run();
        }
    }
}