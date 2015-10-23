using System;
using Windows.ApplicationModel.AppService;
using Windows.Foundation.Collections;
using Windows.UI;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Media;

namespace HeadedApplication
{
    
    public sealed partial class MainPage : Page
    {
        private AppServiceConnection _perimeterBreachService = null;
        public MainPage()
        {
            this.InitializeComponent();
            SetupAppService();
        }

        private async void SetupAppService()
        {
            // find the installed application(s) that expose the app service PerimeterBreachService
            var listing = await AppServiceCatalog.FindAppServiceProvidersAsync("PerimeterBreachService");
            var packageName = "";
            // there may be cases where other applications could expose the same App Service Name, in our case
            // we only have the one
            if (listing.Count == 1)
            {
                packageName = listing[0].PackageFamilyName;
            }
            _perimeterBreachService = new AppServiceConnection();
            _perimeterBreachService.AppServiceName = "PerimeterBreachService";
            _perimeterBreachService.PackageFamilyName = packageName;
            //open app service connection
            var status = await _perimeterBreachService.OpenAsync();

            if (status != AppServiceConnectionStatus.Success)
            {
                //something went wrong
                txtStatus.Text = "Could not connect to the App Service: " + status.ToString();
            }
            else
            {
                //add handler to receive app service messages (Perimiter messages)
                _perimeterBreachService.RequestReceived += PerimeterBreachService_RequestReceived;
            }
        }

        private async void PerimeterBreachService_RequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {

            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, async () => 
            {
                // if you are doing anything awaitable, you need to get a deferral
                var requestDeferral = args.GetDeferral();
                var returnMessage = new ValueSet();
                var uiMessage = "";
                var uiMessageBrush = new SolidColorBrush(Colors.Black);

                try
                {
                    //determine message sent from the background task (perimeter notifications)
                    var message = args.Request.Message["Perimeter Notification"] as string;
                    switch (message)
                    {
                        case "Breached":
                            uiMessage = "Perimeter Breached!!! Alert!!!";
                            uiMessageBrush = new SolidColorBrush(Colors.Red);
                            break;
                        case "Secure":
                            uiMessage = "Perimeter Secured... Move along...";
                            break;
                    }
                    returnMessage.Add("Response", "OK");
                }
                catch (Exception ex)
                {
                    returnMessage.Add("Response", "Failed: " + ex.Message);
                }

                //return OK or Failed response back to the background task
                await args.Request.SendResponseAsync(returnMessage);


                //let the OS know that the action is complete
                requestDeferral.Complete();

                txtStatus.Text = uiMessage;
                txtStatus.Foreground = uiMessageBrush;
            });
        }

        private async void btnCommand_Click(object sender, RoutedEventArgs e)
        {
            Button btn = sender as Button;
            string command = "";
            var message = new ValueSet();
            var statusColor = new SolidColorBrush(Colors.Black);
            switch(btn.Name)
            {
                case "btnLEDOn":
                    command = "Turn LED On";
                    break;
                case "btnLEDOff":
                    command = "Turn LED Off";
                    break;
            }
            message.Add("Request", command);

            //use the app service connection to send LED Command (based on button pressed)
            var response = await _perimeterBreachService.SendMessageAsync(message);
            var result = "";
            //analyze response from the background task
            if(response.Status == AppServiceResponseStatus.Success)
            {
                result = response.Message["Response"] as string;
            }
            else
            {
                result = "Something went wrong: " + response.Status;
                statusColor = new SolidColorBrush(Colors.Red);
            }

            await Dispatcher.RunAsync(Windows.UI.Core.CoreDispatcherPriority.Normal, () =>
            {
                txtStatus.Text = result;
                txtStatus.Foreground = statusColor;
            });
        }
    }
}
