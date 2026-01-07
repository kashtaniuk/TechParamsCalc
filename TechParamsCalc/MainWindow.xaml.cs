using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Media.Animation;
using System.Windows.Media;
using System.Threading;
using TechParamsCalc.DataBaseConnection;
using TechParamsCalc.Controllers;
using TechParamsCalc.UserControls;
using TechParamsCalc.Factory;
using TechParamsCalc.DataBaseConnection.ServerConnections;
using TechParamsCalc.Parameters;
using System.Net;

namespace TechParamsCalc
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private NotifyIcon ni; //Notification for Icon in tray 
        private System.Windows.Controls.ContextMenu TrayMenu;
        public OPCServerSettingsUC opcSettingsUC;
        public DataAccessSettingsUC dataAccessSettingsUC;
        public ParametersUC parametersUC;
        public ServerSynchronizationUC serverSynchronizationUC;
        public LogUC logUC;

        private CalcController controller;
        private bool StateClosed = true;
        private bool mainWindowCanClosed = false;
        //private TimerCallback timerCallBack;
        private HostRole controllerRole;
        private ControllerParameters controllerParameters;
        CancellationTokenSource cancelTokenSource;
        CancellationToken token;

        public MainWindow()
        {

            InitializeComponent();
            ////Application Icon for tray.

            TrayMenu = Resources["TrayMenu"] as System.Windows.Controls.ContextMenu;
            ni = new NotifyIcon();
            string path = AppDomain.CurrentDomain.BaseDirectory;
            ni.Icon = new System.Drawing.Icon(@"Source\icons8_test_tube_white.ico");
            ni.Visible = true;
            ni.DoubleClick +=
                delegate (object sender, EventArgs args)
                {

                    if ((args as MouseEventArgs).Button == MouseButtons.Left)
                    {
                        // по левой кнопке показываем или прячем окно
                        this.Show();
                        this.WindowState = WindowState.Normal;
                    }

                };

            ni.Click += delegate (object sender, EventArgs args)
            {
                if ((args as MouseEventArgs).Button == MouseButtons.Right)
                {
                    // по правой кнопке (и всем остальным) показываем меню
                    TrayMenu.IsOpen = true;
                    Activate(); // нужно отдать окну фокус, см. ниже                    
                }

            };

            //Creating User Interfaces
            opcSettingsUC = new OPCServerSettingsUC { isEnableWritingChecked = true };
            dataAccessSettingsUC = new DataAccessSettingsUC();
            parametersUC = new ParametersUC();
            serverSynchronizationUC = new ServerSynchronizationUC();
            logUC = new LogUC();

            //При запуске приложения проверяется роль хоста (чтение IP из БД). Если при старте компьютера
            //приложение стучится к БД раньше, чем запустится SQL сервер, будет произведена попытка прочитать роль еще раз
            //(кол-во попыток - в теле конструкции 'for')

            Task.Run(() =>
            {
                for (int i = 0; i < 5; i++)
                {
                    controllerRole = DefineHostRole();
                    //Dispatcher.Invoke(() => serverSynchronizationUC.ServerSyncRoleTextBox.Text = controllerRole.ToString());
                    Dispatcher.Invoke(() =>
                    {
                        opcSettingsUC.ServerSyncRoleTextBox.Foreground = Brushes.Black;
                        return opcSettingsUC.ServerSyncRoleTextBox.Content = controllerRole.ToString();
                    });

                    if (controllerRole != HostRole.ERROR)
                        break;
                    WriteToLog("Attempt to define Server role. Reading from DataBase failed!");
                    Thread.Sleep(2000);
                }

            }).ContinueWith((o) =>
            {
                //Создаем класс-контейнер для хранения параметров для передачи в контроллер перед началом расчетов
                controllerParameters = new ControllerParameters
                {
                    isEnableWriting = opcSettingsUC.isEnableWritingChecked,
                    ControllerRole = this.controllerRole,
                    OpcServerName = Properties.Settings.Default.OPCServerName,
                    OpcServerSubstring = Properties.Settings.Default.OPCServerSubString,
                    SingleTagNamesForRW = serverSynchronizationUC.singleTagNamesForRW
                };

                //Start Calculations after launching
                if (controllerRole != HostRole.ERROR)
                    Task.Run(async () => await StartOperation());
                else
                    WriteToLog("Невозможно определить хост-роль сервера");

            });

            
            

        }


        #region MainWidnow Handlers
        private void Window_Loaded(object sender, RoutedEventArgs e)
        {
            //ContentGrid.Children.Add(parametersUC);
            ContentGrid.Children.Add(logUC);
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            ni.Visible = false;
        }

        protected override void OnStateChanged(EventArgs e)
        {
            if (WindowState == WindowState.Minimized)
                this.Hide();

            base.OnStateChanged(e);
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            e.Cancel = !mainWindowCanClosed;
            this.WindowState = WindowState.Minimized;
        }

        private void MenuExitClick(object sender, RoutedEventArgs e)
        {
            mainWindowCanClosed = true;
            Close();
        }

        private void MenuShowClick(object sender, RoutedEventArgs e)
        {
            this.Show();
            this.WindowState = WindowState.Normal;
        }

        private async void MenuStartCalculation(object sender, RoutedEventArgs e)
        {
            //cancelTokenSource.Run();
            
            await StartOperation();
        }

        private async void MenuStopCalculation(object sender, RoutedEventArgs e)
        {
            cancelTokenSource.Cancel();
            await CancelOperation();
        }
        #endregion

        #region Menu Button Handlers
        private void ButtonMenu_Click(object sender, RoutedEventArgs e)
        {
            if (StateClosed)
            {
                Storyboard sb = this.FindResource("OpenMenu") as Storyboard;
                sb.Begin();
                //OPCSetButLabel.Opacity = 1;
            }
            else
            {
                Storyboard sb = this.FindResource("CloseMenu") as Storyboard;
                sb.Begin();
                // OPCSetButLabel.Opacity = 0;
            }

            StateClosed = !StateClosed;
        }
        #endregion

        #region StartCancelCalculationButton Handlers
        private async void StartCalculationButton_Click(object sender, RoutedEventArgs e)
        {
            await StartOperation();
        }

        private async void CancelCalculationButton_Click(object sender, RoutedEventArgs e)
        {
            await CancelOperation();
        }
        #endregion


        //Start Operation
        private async Task StartOperation()
        {
            //Определяем токен для отмены задач
            if (cancelTokenSource != null)
                cancelTokenSource.Dispose();
            cancelTokenSource = new CancellationTokenSource();
            token = cancelTokenSource.Token;

            //Создаем контроллер
            controller = new CalcController(controllerParameters);

            //Подписываемся на события контроллера
            #region Events Subscribe

            //Подписываемся на события прогресса инициализации программы
            controller.progressBarEvent += (o, ea) => { Dispatcher.Invoke(() => ChangeValueProgressBar(((CustomEventArgs)ea).ErrorMessage)); };
            controller.progressBarEvent += (o, ea) => { Dispatcher.Invoke(() => ItemDoneInit(((CustomEventArgs)ea).ErrorMessage)); };

            //Подписываемся на событие возникновения ршибки при записи значений в OPC
            controller.errorRaisedEvent += (o, ea) => { Dispatcher.Invoke(() => WriteToLog(((CustomEventArgs)ea).ErrorMessage)); };

            //Подписываемся на события расчета параметров
            controller.parameterClaculatedSucceedEvent += (o, ea) => ParametersCalculatedFlipper(o, (CustomEventArgs)ea); //Успешный       

            //Подписываемся на событие запроса реинициализации подключения
            controller.reinitalizeClientEvent += async (o, ea) => { await RestartOperation(token); WriteToLog((ea as CustomEventArgs).ErrorMessage); };

            //Подписываемся на события готовности расчетных параметров (Capacity, Density,...)
            controller.capacitiesReadyEvent += (o, ea) =>
            {
                //this.Dispatcher.Invoke(() => parametersUC.ParameterFilterComboBox.ItemsSource = from elem in controller.capacityCreator.nodeElementCollection
                //                                                                                select elem.Name);
            };

            controller.parametersReadyEvent += (o, ea) =>
            {
                //this.Dispatcher.Invoke(() => parametersUC.ParameterFilterComboBox.IsEnabled = true);                
            };

            controller.densitiesReadyEvent += (o, ea) => { };
            controller.contentsReadyEvent += (o, ea) => { };

            #endregion

            //Подключение к OPC
            bool isOPCConnected = await controller.OpcServerConnect();

            if (isOPCConnected)
            {
                Dispatcher.Invoke(() => StartCalculationButton.IsEnabled = false);
            }
            else
            {
                System.Windows.MessageBox.Show("OPC server is unavailable!", "Alert!", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }


            //Создаем списки переменных и инициализируем их данными из OPC
            bool isInitSuccess = false;
            await Task.Run(() =>
            {
                isInitSuccess = controller.InitializeParameters();

            }).ContinueWith((o) => Dispatcher.Invoke(() =>
            {
                //parametersUC.CapacityGrid.ItemsSource = (controller.capacityCreator as CapacityCreator).CapacityList;
                //parametersUC.DensityGrid.ItemsSource = (controller.densityCreator as DensityCreator).DensityList;
                //parametersUC.ContentGrid.ItemsSource = (controller.contentCreator as ContentCreator).ContentList;
                //parametersUC.TankGrid.ItemsSource = (controller.levelTankCreator as LevelTankCreator).LevelTankList;

                parametersUC.CapacityGrid.ItemsSource = (controller.capacityCreator as CapacityCreatorCitect).CapacityList.OrderBy(i => i.TagName);
                parametersUC.DensityGrid.ItemsSource = (controller.densityCreator as DensityCreatorCitect).DensityList.OrderBy(i => i.TagName);
                parametersUC.ContentGrid.ItemsSource = (controller.contentCreator as ContentCreatorCitect).ContentList.OrderBy(i => i.TagName);
                parametersUC.TankGrid.ItemsSource = (controller.levelTankCreator as LevelTankCreatorCitect).LevelTankList.OrderBy(i => i.TagName);

            })
                          );

            if (isInitSuccess)
            {
                //Запускаем процесс расчета                         
                WriteToLog("Server is started!\t");
                controller.CancellationCommand = false;

                try
                {
                    _ = Task.Run(() =>
                                  {
                                      controller.CalculateParameters(token);
                                  });
                }
                catch (Exception e)
                {
                    WriteToLog(e.Message);
                }
            }
            else
            {
                WriteToLog("Can not start calculations due to isInitSuccess = false!\t");
            }
        }

        //Cancel Operation
        private async Task CancelOperation()
        {
            if (controller.opcServer.IsConnected)
            {                      
                controller.CancellationCommand = true;

                await Task.Run(() => controller.opcServer.Disconnect());
                controller.opcServer.Dispose();


                WriteToLog("Server is stopped!\t");

                //Сбрасываем счетчики переменных
                Capacity.id = 0;
                Density.id = 0;
                Parameters.Content.id = 0;
                Dispatcher.Invoke(() => StartCalculationButton.IsEnabled = true);
            }
        }

        //Restart Operation
        private async Task RestartOperation(CancellationToken token)
        {

            cancelTokenSource.Cancel();

            await CancelOperation();

            await StartOperation();

        }

        //Обновляем UI по событию parametersCalculatedEvent контроллера 
        bool isSucceedPreviuos = false;
        private void ParametersCalculatedFlipper(object sender, CustomEventArgs args)
        {

            //Обновляем DataGrid'ы с параметрами
            this.Dispatcher.Invoke(() =>
            {
                parametersUC.CapacityGrid.Items.Refresh();
                parametersUC.DensityGrid.Items.Refresh();
                parametersUC.ContentGrid.Items.Refresh();
                parametersUC.TankGrid.Items.Refresh();
            });


            //Закрашиваем rectangle расчета в цвет (зеленый - если расчет Ок, красный - расчет не Ок. Желтый - если Ок и инстанс программы "слушает" OPC)
            this.Dispatcher.Invoke(() =>
            {
                //Если произошла ошибка записи/чтения
                if (!args.IsSucceed && isSucceedPreviuos)
                    //Подменяем иконку в трее на Failed 
                    ni.Icon = new System.Drawing.Icon(@"Source\icons8_test_tube_failed.ico");


                //Если ушла ошибка записи/чтения
                if (args.IsSucceed && !isSucceedPreviuos)
                    //Подменяем иконку в трее на нормальную - без ошибок                    
                    ni.Icon = new System.Drawing.Icon(@"Source\icons8_test_tube_white.ico");


                if (args.IsSucceed)
                    isSucceedPreviuos = true;

                if (args.IsSucceed && controller.IsInstanceActive)
                    Version.Foreground = Brushes.Green;

                if (args.IsSucceed && !controller.IsInstanceActive)
                    Version.Foreground = Brushes.Yellow;


                ServerSyncRect1.Content    = (controller.singleTagCreator as SingleTagCreator).SingleTagFromPLC[3] == 1 ? "online" : "offline";
                ServerSyncRect1.Background = (controller.singleTagCreator as SingleTagCreator).SingleTagFromPLC[3] == 1 ? Brushes.Green : (SolidColorBrush)new BrushConverter().ConvertFrom("#FF808080");

                ServerSyncRect2.Content    = (controller.singleTagCreator as SingleTagCreator).SingleTagFromPLC[4] == 1 ? "online" : "offline";
                ServerSyncRect2.Background = (controller.singleTagCreator as SingleTagCreator).SingleTagFromPLC[4] == 1 ? Brushes.Green : (SolidColorBrush)new BrushConverter().ConvertFrom("#FF808080");


            if (!args.IsSucceed)
                {
                    Version.Foreground = Brushes.Red;
                    WriteToLog("Writing Error!!!!\t" + args.ErrorMessage);
                    isSucceedPreviuos = false;
                }

            });

            //Обновляем запись о величине атмосферного давления от метеостанции
            this.Dispatcher.Invoke(() => parametersUC.AtmoPressureLabel.Content = (controller.singleTagCreator as SingleTagCreator).AtmoPressureFromOPC * 0.0001);

            Thread.Sleep(800);
            this.Dispatcher.Invoke(() => Version.Foreground = (SolidColorBrush)new BrushConverter().ConvertFrom("#FF808080")); //Brushes.LightGray);

            //Индикация активности текущего инстанса программы на предмет записи в OPC
            this.Dispatcher.Invoke(() =>
            {
                //serverSynchronizationUC.ServerSyncWritingRectangle.Fill = controller.IsInstanceActive ? Brushes.Green : Brushes.Gray;
                opcSettingsUC.ServerSyncWritingRectangle.Background = controller.IsInstanceActive ? Brushes.Green : Brushes.Red;
                opcSettingsUC.ServerSyncWritingRectangle.Content = controller.IsInstanceActive ? "Writable" : "Read only";

                //opcSettingsUC.ServerStateRectangle.Fill = controller.opcServer.IsConnected ? Brushes.Green : Brushes.Yellow;
                opcSettingsUC.ServerStateRectangle.Background = controller.opcServer.IsConnected ? Brushes.Green : Brushes.Red;
                opcSettingsUC.ServerStateRectangle.Content = controller.opcServer.IsConnected ? "Connected" : "Not connected";
            });

            //Обновляем параметр isEnableWriting класса-контейнера ControllerParameters для определения может ли писать инстанс программы в OPC
            controllerParameters.isEnableWriting = opcSettingsUC.isEnableWritingChecked;
        }

        //Определяем свой ip и свою роль в процессе чтения-записи в (из) OPC
        private HostRole DefineHostRole()
        {
            List<AuthorizedHost> hostsAddressList = null;
            //Checking for Autorized hostAddresses for writing to OPC (reading from Data Base)
            try
            {
                using (DBPGContext dbContext = new DBPGContext())
                {
                    hostsAddressList = (from h in dbContext.contentAutorizedServers
                                        select h).ToList();
                }
            }
            catch (Exception)
            {                
                return HostRole.ERROR;
            }

            hostsAddressList.Sort((x, y) => x.id.CompareTo(y.id));
            if (hostsAddressList == null)
                return HostRole.UNKNOWN;


            //IP addresses from Data Base
            IPAddress[] dBHostIpAddresses = new IPAddress[hostsAddressList.Count()];
            for (int i = 0; i < dBHostIpAddresses.Length; i++)
            {
                IPAddress.TryParse(hostsAddressList[i].hostAddress, out dBHostIpAddresses[i]);
            }

            //Local Machine IP addresses
            IPAddress[] localHostIpAddresses = Dns.GetHostAddresses(Dns.GetHostName());

            //Check for consistance IP Address in DB and IP Address of Local Machine
            if (dBHostIpAddresses.Length > 0 && localHostIpAddresses.Contains(dBHostIpAddresses[0]))
                return HostRole.PRIMARY_SERVER;

            if (dBHostIpAddresses.Length > 1 && localHostIpAddresses.Contains(dBHostIpAddresses[1]))
                return HostRole.SECONDARY_SERVER;

            return HostRole.CLIENT;
        }

        //Запись в Лог
        private void WriteToLog(string logText)
        {
            //Запись в TextBlock UI
            this.Dispatcher.Invoke(() => logUC.LogTextBlock.Text = logUC.LogTextBlock.Text + DateTime.Now.ToLongTimeString() + @"	    " + logText + Environment.NewLine);

            //Запись в файл
            try
            {
                File.AppendAllText(@"log.txt", DateTime.Now.ToString() + "\t" + logText + Environment.NewLine);
            }
            catch (DirectoryNotFoundException)
            {
                this.Dispatcher.Invoke(() => logUC.LogTextBlock.Text = logUC.LogTextBlock.Text + "Log File not found. Please check the file path! (Controllers/log.txt)" + DateTime.Now.ToString().PadLeft(40) + Environment.NewLine);
            }
        }

        // Изменение значения прогресс бара
        private void ChangeValueProgressBar(string progressBarValue)
        {
            //Запись в UI
            this.Dispatcher.Invoke(() => ProgressBarInit.Value = Convert.ToDouble(progressBarValue));
            if (Convert.ToDouble(progressBarValue) == 100)
            {
                ProgressBarInit.Visibility = Visibility.Hidden;
                TextProgressBarInit.Visibility = Visibility.Hidden;
            }
        }

        private void ItemDoneInit(string progressBarValue)
        {
            switch (progressBarValue)
            {
                case "1":
                    this.Dispatcher.Invoke(() => logUC.DBTextResult.Content = "done");
                    break;
                case "2":
                    this.Dispatcher.Invoke(() => logUC.SingleTextCount.Content = controller.countSingleTags);
                    this.Dispatcher.Invoke(() => logUC.SingleTextResult.Content = "done");
                    break;
                case "14":
                    this.Dispatcher.Invoke(() => logUC.TemperatureTextCount.Content = controller.countTemperatures);
                    this.Dispatcher.Invoke(() => logUC.TemperatureTextResult.Content = "done");
                    break;
                case "24":
                    this.Dispatcher.Invoke(() => logUC.PressureTextCount.Content = controller.countPressures);
                    this.Dispatcher.Invoke(() => logUC.PressureTextResult.Content = "done");
                    break;
                case "34":
                    this.Dispatcher.Invoke(() => logUC.LevelsTextCount.Content = controller.countLevels);
                    this.Dispatcher.Invoke(() => logUC.LevelsTextResult.Content = "done");
                    break;
                case "44":
                    this.Dispatcher.Invoke(() => logUC.CapacitiesTextCount.Content = controller.countCapacities);
                    this.Dispatcher.Invoke(() => logUC.CapacitiesTextResult.Content = "done");
                    break;
                case "54":
                    this.Dispatcher.Invoke(() => logUC.DensitiesTextCount.Content = controller.countDensities);
                    this.Dispatcher.Invoke(() => logUC.DensitiesTextResult.Content = "done");
                    break;
                case "64":
                    this.Dispatcher.Invoke(() => logUC.ContentsTextCount.Content = controller.countContents);
                    this.Dispatcher.Invoke(() => logUC.ContentsTextResult.Content = "done");
                    break;
                case "74":
                    this.Dispatcher.Invoke(() => logUC.TankTextCount.Content = controller.countTanks);
                    this.Dispatcher.Invoke(() => logUC.TankTextResult.Content = "done");
                    break;
                case "100":
                    this.Dispatcher.Invoke(() => logUC.ServerTextResult.Content = "done");
                    break;
                default:
                    break;
            }

        }

        //Обработчик событий кнопок Hamburger Menu
        private void HamburgerMenuButton_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Controls.Button button = (System.Windows.Controls.Button)sender;

            ContentGrid.Children.Clear();
            switch (button.Name)
            {

                case "HomeButton":
                    ContentGrid.Children.Add(parametersUC);
                    break;
                case "OPCSetButton":
                    ContentGrid.Children.Add(opcSettingsUC);
                    break;
                case "DataSetButton":
                    ContentGrid.Children.Add(dataAccessSettingsUC);
                    break;
                case "NetSetButton":
                    ContentGrid.Children.Add(serverSynchronizationUC);
                    break;
                case "LogButton":
                    ContentGrid.Children.Add(logUC);
                    break;

                default:
                    break;
            }

        }


    }
}

