using System;
using System.Threading.Tasks;
using Windows.ApplicationModel.AppService;
using Windows.ApplicationModel.Background;
using Windows.Devices.Gpio;
using Windows.Foundation.Collections;

namespace BackgroundHeadlessApplication
{
    public sealed class PerimeterBreachAppService : IBackgroundTask
    {
        private BackgroundTaskDeferral _deferral;
        private AppServiceConnection _connection;
        private int _sensorPinNumber = 18;
        private GpioPin _sensorPin;
        private int _ledPinNumber = 23;
        private GpioPin _ledPin;

        public void Run(IBackgroundTaskInstance taskInstance)
        {
            //keep this background task alive
            _deferral = taskInstance.GetDeferral();
            taskInstance.Canceled += OnTaskCanceled;

            //execution triggered by another application requesting this App Service
            //assigns an event handler to fire when a message is received from the client
            var triggerDetails = taskInstance.TriggerDetails as AppServiceTriggerDetails;
            _connection = triggerDetails.AppServiceConnection;
            _connection.RequestReceived += Connection_RequestReceived;

            //initialize sensor and led pins
            GpioController controller = GpioController.GetDefault();
            _sensorPin = controller.OpenPin(_sensorPinNumber);
            _sensorPin.SetDriveMode(GpioPinDriveMode.InputPullUp); //high when door is open, low when closed
            _sensorPin.ValueChanged += SensorPin_ValueChanged; //assigns event handler to fire when 
            _ledPin = controller.OpenPin(_ledPinNumber);
            _ledPin.SetDriveMode(GpioPinDriveMode.Output);

            //send initial notification of the startup state of the sensor
            //toggle current value so initial notification is made
            NotifyClientsOfPerimeterState().Wait();
        }

        private async void SensorPin_ValueChanged(GpioPin sender, GpioPinValueChangedEventArgs args)
        {
            await NotifyClientsOfPerimeterState();
        }

        private async Task NotifyClientsOfPerimeterState()
        {
            var _sensorCurrentValue = _sensorPin.Read();
            var messages = new ValueSet(); //name value pair

            if (_sensorCurrentValue == GpioPinValue.High)
            {
                //send perimeter breached
                messages.Add("Perimeter Notification", "Breached");
            }
            else
            {
                //send perimeter secure
                messages.Add("Perimeter Notification", "Secure");
            }

            //send message to the client
            var response = await _connection.SendMessageAsync(messages);

            if (response.Status == AppServiceResponseStatus.Success)
            {
                var result = response.Message["Response"];
                //optionally log result from client
            }
        }


        private async void Connection_RequestReceived(AppServiceConnection sender, AppServiceRequestReceivedEventArgs args)
        {
            // if you are doing anything awaitable, you need to get a deferral
            var requestDeferral = args.GetDeferral();
            var returnMessage = new ValueSet();
            try
            {
                //obtain and react to the command passed in by the client
                var message = args.Request.Message["Request"] as string;
                switch (message)
                {
                    case "Turn LED On":
                        _ledPin.Write(GpioPinValue.High);
                        break;
                    case "Turn LED Off":
                        _ledPin.Write(GpioPinValue.Low);
                        break;
                }
                returnMessage.Add("Response", "OK");
            }
            catch (Exception ex)
            {
                returnMessage.Add("Response", "Failed: " + ex.Message);
            }

            await args.Request.SendResponseAsync(returnMessage);
 
            //let the OS know that the action is complete
            requestDeferral.Complete();
        }

        private void OnTaskCanceled(IBackgroundTaskInstance sender, BackgroundTaskCancellationReason reason)
        {
            if (_deferral != null)
            {
                _deferral.Complete();
            }
        }
    }
}
