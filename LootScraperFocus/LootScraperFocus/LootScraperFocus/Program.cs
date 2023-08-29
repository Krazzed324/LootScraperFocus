/*
Discord bot developed by Krazzed324 for Team Focus



*/

using NLog;
using Discord.Net;
using Discord.Rest;
using Discord;
using Discord.WebSocket;
using System.Text.RegularExpressions;
using System.Globalization;
using System.Configuration;

using Google.Apis.Sheets.v4;
using Google.Apis.Auth.OAuth2;
using Google.Apis.Util.Store;
using Google.Apis.Services;
using Google.Apis.Sheets.v4.Data;

public class Program
{
    readonly Logger log = LogManager.GetCurrentClassLogger();
    private DiscordSocketClient client;

    private Regex messageFormat;

    string ArchiveDirectory = ConfigurationManager.AppSettings["Local.Directories.ArchiveFolder"];

    static string[] Scopes = { SheetsService.Scope.Spreadsheets };

    readonly string testing_Channel_ID = "1141051041014628453"; // Testing channel in Team Focus discord

    readonly string channel_ID = "1142118517500555304"; // Production value for log channel
    static readonly string spreadsheet_info = "https://docs.google.com/spreadsheets/d/1CzVtmzwBRIYJhDRdIERn16cZYxUNQm5Q_bgRqVdglD4/edit#git=1589008516"; // SpreadsheetID from physical document. 

    static readonly string spreadsheetID = "1CzVtmzwBRIYJhDRdIERn16cZYxUNQm5Q_bgRqVdglD4";
    static readonly string pageID = "1589008516";
    //static readonly string range = "Log!A";
    //New range building per multiple spot upload
    static readonly string rangeStart = "Log!A";
    static readonly string rangeEnd = "D";

    static readonly string ReadFromLocation = "Log!E1";
    static readonly string applicationName = "3.22 TF PL";


    public static Task Main(string[] args) => new Program().MainAsync();


    public async Task MainAsync()
    {

        

        _ = Directory.CreateDirectory(ArchiveDirectory);

        var config = new DiscordSocketConfig();
        config.GatewayIntents = GatewayIntents.All;
        client = new DiscordSocketClient(config);

        client.Log += Log;

        var token = "MTEzNjg4NTY4MDU2MDE1NjcwMw.GxXZNn.Q71Do7sc45qw8XbzY-05ojaxBLMoUznnif0Owc"; // TODO - need to grab token from discord api stuff (for patches when reading this.... yes this is how I comment shit deal with it)

        await client.LoginAsync(TokenType.Bot, token);
        await client.StartAsync();
        

        //SpreadsheetsResource resource = new SpreadsheetsResource();
        



        //TODO - Need to figure out event hook here for ClientOnMessageReceived and make sure it only is looking in a specific channel
        client.MessageReceived += ClientOnMessageReceived;

        //Delay this task until program is done.
        await Task.Delay(-1);
    }


    //Method that will handle reading context from specific channel and upload correctly formatted messages to google sheet (via Rest api when I can be bothered to read the google sheets documentation)
    private async Task ClientOnMessageReceived(SocketMessage message)
    {
        NumberFormatInfo nfi = CultureInfo.GetCultureInfo("en-US").NumberFormat;
        

        //Pattern must be split here to properly handle escape. I hate it but we do what we must (also I am too lazy to figure out a better implementation)
        string pattern = "^.*";
        pattern += Regex.Escape(nfi.PositiveSign) + @"\d+\s.+";

        string splitPattern = Regex.Escape(nfi.PositiveSign) + @"\d+";

        Regex splitRegex = new Regex(splitPattern);
        
        messageFormat = new Regex(pattern);

        _ = Task.Run(async () =>
        {
            if (!message.Author.IsBot)
            {
                //Compare message data to match (also grab other important information like author, message time etc for requirements per conversation with PatchesMcDoogal on 08/04/2023 (American date style)
                var timestamp = message.Timestamp;
                var userID = message.Author.Id;
                var userName = message.Author.Username;
                var messageID = message.Id;
                var messageData = message.Content;
                var match = messageFormat.Match(messageData);
                Console.WriteLine((message.Channel.Id.ToString().Equals(channel_ID, StringComparison.OrdinalIgnoreCase) || message.Channel.Id.ToString().Equals(testing_Channel_ID, StringComparison.OrdinalIgnoreCase)));
                if (match.Success && (message.Channel.Id.ToString().Equals(channel_ID, StringComparison.OrdinalIgnoreCase) || message.Channel.Id.ToString().Equals(testing_Channel_ID, StringComparison.OrdinalIgnoreCase)))
                {
                    var splitMessage = splitRegex.Split(messageData, 2);
                    //Here be dragons (where we putting logic to dump to google doc)
                    UserCredential credential;

                    using (var stream =
                        new FileStream("client_secret.json", FileMode.Open, FileAccess.Read))
                    {
                        string credPath = System.Environment.GetFolderPath(
                            System.Environment.SpecialFolder.Personal);


                        credential = GoogleWebAuthorizationBroker.AuthorizeAsync(
                            GoogleClientSecrets.FromStream(stream).Secrets,
                            Scopes,
                            "user",
                            CancellationToken.None,
                            new FileDataStore(credPath, true)).Result;

                        var service = new SheetsService(new BaseClientService.Initializer()
                        {
                            HttpClientInitializer = credential,
                            ApplicationName = applicationName,
                        });


                        SpreadsheetsResource.ValuesResource.GetRequest getRequest = service.Spreadsheets.Values.Get(spreadsheetID, ReadFromLocation);
                        ValueRange response = getRequest.Execute();

                        Console.WriteLine(response.Values.FirstOrDefault().FirstOrDefault().ToString());
                        //log.Info(response.ToString());

                        ValueRange valueRange = new ValueRange();
                        valueRange.MajorDimension = "ROWS";
                        //Split result should return 2 values, anything preleading +(digits) and then whatever is afterwards
                        //Per new scope creep function from Llew we are now splitting into 4 values that we need to update
                        var oblist = new List<Object>() { $"{splitMessage[1]}", Int16.Parse(GetDigit(messageData)), $"{userName}", $"{timestamp}" };
                        valueRange.Values = new List<IList<object>> { oblist };
                        Console.WriteLine(oblist.ToString());
                        SpreadsheetsResource.ValuesResource.UpdateRequest updateRequest = service.Spreadsheets.Values.Update(valueRange, spreadsheetID, $"{rangeStart}{response.Values.FirstOrDefault().FirstOrDefault().ToString()}:{rangeEnd}{response.Values.FirstOrDefault().FirstOrDefault().ToString()}");
                        updateRequest.ValueInputOption = SpreadsheetsResource.ValuesResource.UpdateRequest.ValueInputOptionEnum.RAW;
                        UpdateValuesResponse updateResponse = updateRequest.Execute();


                    }
                }

            }
            

        });
        return;
    }

    private Task Log(LogMessage msg)
    {
        _ = Directory.CreateDirectory(Path.Combine(ArchiveDirectory, DateTime.Now.Year.ToString(), DateTime.Now.Month.ToString(), DateTime.Now.Day.ToString()));
        if (msg.Message != null)
        {
            log.Info(msg.Message.ToString());
        }


        return Task.CompletedTask;
    }


    private string GetDigit(string message)
    {
        NumberFormatInfo nfi = CultureInfo.GetCultureInfo("en-US").NumberFormat;

        //Use a lookbehind split to not consume the digits in the input string
        string splitPattern = @"(?<=\d+)";

        Regex splitRegex = new Regex(splitPattern);
        string data = splitRegex.Split(message)[0];
        Console.WriteLine(data.Substring(1));
        return data.Substring(1);
    }
}