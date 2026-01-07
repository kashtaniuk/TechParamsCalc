using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using TitaniumAS.Opc.Client.Common;
using TitaniumAS.Opc.Client.Da;
using TechParamsCalc.Parameters;
using TechParamsCalc.OPC;
using TechParamsCalc.Factory;
using TechParamsCalc.DataBaseConnection;
using TechParamsCalc.DataBaseConnection.ServerConnections;
using TitaniumAS.Opc.Client.Da.Browsing;

using System.Windows;


//Main Calculation Controller

namespace TechParamsCalc.Controllers
{

    internal class CalcController
    {
        internal OpcDaServer opcServer;
        internal IOpcClient opcClient;

        internal ItemsCreator capacityCreator;
        internal ItemsCreator densityCreator;
        internal ItemsCreator contentCreator;
        internal ItemsCreator levelTankCreator;

        internal ItemsCreator temperatureCreator;
        internal ItemsCreator pressureCreator;
        internal ItemsCreator levelCreator;
        internal ItemsCreator singleTagCreator;

        public List<OpcDaBrowseElement> NodeElementCollectionFromOPC { get; set; }

        public event EventHandler parameterClaculatedSucceedEvent;

        public event EventHandler capacitiesReadyEvent;
        public event EventHandler densitiesReadyEvent;
        public event EventHandler contentsReadyEvent;

        public event EventHandler parametersReadyEvent;
        public event EventHandler reinitalizeClientEvent;
        public event EventHandler errorRaisedEvent;
        public event EventHandler progressBarEvent;

        public List<DataBaseConnection.Capacity.CapacityContent> listOfCapacityFromDB;
        public List<DataBaseConnection.Density.DensityContent> listOfDensityFromDB;
        public List<DataBaseConnection.Content.ContentContent> listOfContentFromDB;
        public List<DataBaseConnection.Level.TankContent> listOfLevelFromDB;
        //public List<DataBaseConnection.Level.Tank> listOfTankFromDB;

        public HashSet<String> sumListCapacityFromDB = new HashSet<string>();
        public HashSet<String> sumListDensityFromDB = new HashSet<string>();
        public HashSet<String> sumListContentFromDB = new HashSet<string>();
        public HashSet<String> sumListTemperatureFromDB = new HashSet<string>();
        public HashSet<String> sumListPressureFromDB = new HashSet<string>();
        public HashSet<String> sumListLevelsFromDB = new HashSet<string>();

        public int countSingleTags;
        public int countTemperatures;
        public int countPressures;
        public int countLevels;
        public int countCapacities;
        public int countDensities;
        public int countContents;
        public int countTanks;

        //private AdditionalCalculator additionalCalculator;
        private const int maxAttemptsCount = 5;

        private bool isServerPrimaryFlipping;      //Изменяется ли в PLC тег, который сигнализирует о том, что в PLC один из экземпляров программы пишет инфо
        private bool isServerSecondaryFlipping;    //Изменяется ли тег от Secondary Server
        private ControllerParameters controllerParameters;

        private HostRole controllerRole;    //Primary Server, Secondary Server, Client
        private int pollingPeriod = 2000;
        public bool IsInstanceActive { get; set; } //Маркер, сигнализирующий о том, пишет ли данный экземлпяр программы в PLC или находится в режиме StandAlone

        public string[] SingleTagsNamesForRW { get; private set; }
        public bool CancellationCommand { get; set; } //true - когда счетчик AtemtsCount равен установленному максимальному значению
        public int AtemtsCount { get; set; } //Счетчик попыток установить связь с OPC- сервером. После достиженя уставки происходит реинициализация подключения с OPC и BD        

        private Stopwatch timer;
        public CalcController(ControllerParameters controllerParameters)
        {
            this.controllerRole = controllerParameters.ControllerRole;
            timer = new Stopwatch();
            Uri url = UrlBuilder.Build(controllerParameters.OpcServerName);

            opcServer = new OpcDaServer(url);
            //opcClient = new OpcClient(opcServer, controllerParameters.OpcServerSubstring); // для OFC-client
            opcClient = new OpcClientCitect(opcServer, controllerParameters.OpcServerSubstring); // для OPC Citect-client
            SingleTagsNamesForRW = controllerParameters.SingleTagNamesForRW;

            this.controllerParameters = controllerParameters;

            //Определение creator'ов для переменных

            //0.0. Single Tag for reading/writing to OPC
            singleTagCreator = new SingleTagCreator(opcClient, SingleTagsNamesForRW);
            //1.0. TemperatureCreator
            temperatureCreator = new TemperatureCreator(opcClient);
            //2.0. PressureCreator
            pressureCreator = new PressureCreator(opcClient);
            //     levelCreator
            levelCreator = new LevelCreator(opcClient);
            //3.0. CapacityCreator
            capacityCreator = new CapacityCreatorCitect(opcClient, singleTagCreator);
            //4.0. DensityCreator
            densityCreator = new DensityCreatorCitect(opcClient, singleTagCreator);
            //5.0. ContentCreator
            contentCreator = new ContentCreatorCitect(opcClient, singleTagCreator);
            //6.0. ContentCreator
            levelTankCreator = new LevelTankCreatorCitect(opcClient, singleTagCreator);



            //parameterClaculatedSucceedEvent += (o, ea) => CheckForItemInPlcForWritingToPLC(o, (CustomEventArgs)ea);

            //Запускаем процесс проверки изменения тега в PLC для определения разрешения записи данному экземпляру программы в PLC
            //Процесс непрерывно раз в 3 периода поллинга проверяет активность тега PLC
            Task.Run(() => CheckForItemInPlcForWritingToPLC());
        }


        //Подключение к OPC
        internal async Task<bool> OpcServerConnect()
        {
            await Task.Run(() =>
            {
                try
                {
                    // Connect to the server first.
                    if (!opcServer.IsConnected)
                    {
                        opcServer.Connect();
                        errorRaisedEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "Connect to OPC Server - ok" });
                        progressBarEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "1" });
                    }
                }
                catch (Exception)
                {
                    MessageBox.Show("Can`t connect to OPC", "Alert", MessageBoxButton.OK, MessageBoxImage.Error);
                }

            });
            return opcServer.IsConnected;
        }

        //Метод для создания и инициализации параметров из OPC 
        internal bool InitializeParameters()
        {

            bool isiInitSuccess = true;

            #region 0. OpcStatusInfo From PLC + AtmoPressure    

            //0.2. Формируем группы чтения для OPC-сервера       
            singleTagCreator.CreateOPCReadGroup();
            errorRaisedEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "Create single tag OPC read group" });

            //0.3. Обновленная данными из OPC переменная singleTagCreator.SingleTagFromPLC
            try
            {
                singleTagCreator.UpdateItemListFromOpc();
                countSingleTags = singleTagCreator.countItems;
                errorRaisedEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "Single tag - ok" });
                progressBarEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "2" });
            }
            catch (Exception e)
            {
                if (errorRaisedEvent != null)
                    errorRaisedEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "Single tag OPC reading error" + $" {e.Message }" });
                isiInitSuccess = false;
            }


            //0.5. Формируем группы записи для OPC-сервера
            (singleTagCreator as SingleTagCreator).CreateOPCWriteGroup(controllerRole == HostRole.PRIMARY_SERVER);
            errorRaisedEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "Create Single tag OPC write group" });
            progressBarEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "3" });

            #endregion

            #region Read data from OPC and DB PG

            // вичитываем ВСЕ переменные из ОРС сервера и сохраняем в NodeElementCollectionFromOPC
            CreateItemListFromOPC();
            errorRaisedEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "Get all tags from OPC" });
            progressBarEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "4" });

            // формируем список необходимых температур из базы данных DB PG
            ReadCapacityDataFromDB();
            errorRaisedEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "Read capacity from DB" });
            progressBarEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "5" });
            ReadDensityDataFromDB();
            errorRaisedEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "Read density from DB" });
            progressBarEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "6" });
            ReadContentDataFromDB();
            errorRaisedEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "Read content from DB" });
            progressBarEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "7" });
            ReadLevelDataFromDB();
            errorRaisedEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "Read levels from DB" });
            progressBarEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "8" });

            // формируем list с температурами из БД
            GetTemperatureFromDB();
            errorRaisedEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "Create list with temperature" });
            progressBarEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "9" });

            // формируем list с давлениями из БД
            GetPressureFromDB();
            errorRaisedEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "Create list with pressure" });
            progressBarEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "10" });

            // формируем list с уровнями из БД
            //GetLevelFromDB();

            #endregion

            #region 1. Temperatures            

            //1.1. Пустой список переменных Temperature
            //temperatureCreator.CreateItemList();
            temperatureCreator.CreateItemList(sumListTemperatureFromDB, NodeElementCollectionFromOPC);
            errorRaisedEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "Create temperature list" });
            progressBarEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "11" });

            //1.2. Формируем группы чтения для OPC-сервера
            temperatureCreator.CreateOPCReadGroup();
            errorRaisedEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "Create temperature OPC read group" });
            progressBarEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "12" });

            //1.3. Обновленный данными из OPC список переменных Temperature
            try
            {
                temperatureCreator.UpdateItemListFromOpc();
                countTemperatures = temperatureCreator.countItems;
                errorRaisedEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "Temperatures - ok" });
                progressBarEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "14" });
            }
            catch (Exception e)
            {
                if (errorRaisedEvent != null)
                    errorRaisedEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "Temperature OPC reading error" + $" {e.Message }" });
                isiInitSuccess = false;
            }
            #endregion

            #region 2. Pressures           

            //2.1. Пустой список переменных Pressure
            //pressureCreator.CreateItemList();
            pressureCreator.CreateItemList(sumListPressureFromDB, NodeElementCollectionFromOPC);
            errorRaisedEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "Create pressure list" });
            progressBarEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "20" });

            //2.2. Формируем группы чтения для OPC-сервера
            pressureCreator.CreateOPCReadGroup();
            errorRaisedEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "Create pressure OPC read group" });
            progressBarEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "22" });

            //2.3. Обновленный данными из OPC список переменных Pressure
            try
            {
                pressureCreator.UpdateItemListFromOpc();
                countPressures = pressureCreator.countItems;
                errorRaisedEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "Pressures - ok" });
                progressBarEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "24" });
            }
            catch (Exception e)
            {
                if (errorRaisedEvent != null)
                    errorRaisedEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "Pressures OPC reading error" + $" {e.Message }" });
                isiInitSuccess = false;
            }

            #endregion

            #region Levels
            //1.1. Пустой список переменных Level (strings с наименованием тегов)
            //levelCreator.CreateItemList();
            levelCreator.CreateItemList(sumListLevelsFromDB, NodeElementCollectionFromOPC);
            errorRaisedEvent.Invoke(this, new CustomEventArgs { ErrorMessage = $"Create levels list" });
            progressBarEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "30" });

            //1.2. Формируем группы чтения для OPC-сервера
            levelCreator.CreateOPCReadGroup();
            errorRaisedEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "Create levels OPC read group" });
            progressBarEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "32" });

            //1.3. Обновленный данными из OPC список переменных Temperature
            try
            {
                levelCreator.UpdateItemListFromOpc();
                countLevels = levelCreator.countItems;

                errorRaisedEvent.Invoke(this, new CustomEventArgs { ErrorMessage = $"Levels - ok" });
                progressBarEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "34" });
            }
            catch (Exception e)
            {
                if (errorRaisedEvent != null)
                    errorRaisedEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "Level OPC reading error" + $" {e.Message }" });
                isiInitSuccess = false;
            }
            #endregion

            #region 3. Capacities            

            //3.1. Пустой список переменных Capacity
            //capacityCreator.CreateItemList();
            capacityCreator.CreateItemList(sumListCapacityFromDB, NodeElementCollectionFromOPC);
            errorRaisedEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "Create capacity list" });
            progressBarEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "40" });

            //3.2. Формируем группы чтения для OPC-сервера
            capacityCreator.CreateOPCReadGroup();
            errorRaisedEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "Create capacity OPC read group" });
            progressBarEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "42" });

            //3.3. Обновленный данными из OPC список переменных Capacity
            try
            {
                capacityCreator.UpdateItemListFromOpc();
                countCapacities = capacityCreator.countItems;
                errorRaisedEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "Capacities - ok" });
                progressBarEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "44" });
            }
            catch (Exception e)
            {
                if (errorRaisedEvent != null)
                    errorRaisedEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "Capacities OPC reading error" + $" {e.Message }" });
                isiInitSuccess = false;
            }


            //3.4. Обновленный данными из DB список переменных Capacity
            try
            {
                using (DBPGContext dbcontext = new DBPGContext())
                {
                    capacityCreator.UpdateItemListFromDB((temperatureCreator as TemperatureCreator).TemperatureList, (pressureCreator as PressureCreator).PressureList, dbcontext); //Заполененный данными из DB список переменных                
                }
                errorRaisedEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "Get capacity data from DB" });
                progressBarEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "46" });
            }
            catch (Exception e)
            {
                if (errorRaisedEvent != null)
                    errorRaisedEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "Error in DB Capacity" + $" {e.Message }" });
                isiInitSuccess = false;
            }

            //3.5. Формируем группы записи для OPC-сервера
            capacityCreator.CreateOPCWriteGroup();
            errorRaisedEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "Create capacity OPC write group" });
            progressBarEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "48" });

            //3.6. Сообщаемм MainWindow о том, что Capacities сформировались
            if (capacitiesReadyEvent != null)
                capacitiesReadyEvent.Invoke(this, new EventArgs());
            #endregion

            #region 4. Densities
            //4.1. Пустой список переменных Density
            //densityCreator.CreateItemList();
            densityCreator.CreateItemList(sumListDensityFromDB, NodeElementCollectionFromOPC);
            errorRaisedEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "Create density list" });
            progressBarEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "50" });

            //4.2. Формируем группы чтения для OPC-сервера
            densityCreator.CreateOPCReadGroup();
            errorRaisedEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "Create density OPC read group" });
            progressBarEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "52" });

            //4.3. Обновленный данными из OPC список переменных Density
            try
            {
                densityCreator.UpdateItemListFromOpc();
                countDensities = densityCreator.countItems;
                errorRaisedEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "Densities - ok" });
                progressBarEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "54" });
            }
            catch (Exception e)
            {
                if (errorRaisedEvent != null)
                    errorRaisedEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "Densities OPC reading error" + $" {e.Message }" });
                isiInitSuccess = false;
            }


            //4.4. Обновленный данными из DB список переменных Density
            try
            {
                using (DBPGContext dbcontext = new DBPGContext())
                {
                    densityCreator.UpdateItemListFromDB((temperatureCreator as TemperatureCreator).TemperatureList, (pressureCreator as PressureCreator).PressureList, dbcontext); //Заполененный данными из DB список переменных                
                }
                errorRaisedEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "Get density data from DB" });
                progressBarEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "56" });
            }
            catch (Exception e)
            {
                if (errorRaisedEvent != null)
                    errorRaisedEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "Error in DB Density" + $" {e.Message }" });
                isiInitSuccess = false;
            }

            //4.5. Формируем группы записи для OPC-сервера
            densityCreator.CreateOPCWriteGroup();
            errorRaisedEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "Create density OPC write group" });
            progressBarEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "58" });

            //4.6. Сообщаемм MainWindow о том, что Densities сформировались и ими можно наполнять ComboBox в ParametersUC
            if (densitiesReadyEvent != null)
                densitiesReadyEvent.Invoke(this, new EventArgs());

            #endregion

            #region 5. Contents
            //5.1. Пустой список переменных Content
            //contentCreator.CreateItemList();
            contentCreator.CreateItemList(sumListContentFromDB, NodeElementCollectionFromOPC);
            errorRaisedEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "Create content list" });
            progressBarEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "60" });

            //5.2. Формируем группы чтения для OPC-сервера
            contentCreator.CreateOPCReadGroup();
            errorRaisedEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "Create content OPC read group" });
            progressBarEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "62" });

            //5.3. Обновленный данными из OPC список переменных Content
            try
            {
                contentCreator.UpdateItemListFromOpc();
                countContents = contentCreator.countItems;
                errorRaisedEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "Contents - ok" });
                progressBarEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "64" });
            }
            catch (Exception e)
            {
                if (errorRaisedEvent != null)
                    errorRaisedEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "Contents OPC reading error" + $" {e.Message }" });
                isiInitSuccess = false;
            }


            //5.4. Обновленный данными из DB список переменных Content
            try
            {
                using (DBPGContext dbcontext = new DBPGContext())
                {
                    contentCreator.UpdateItemListFromDB((temperatureCreator as TemperatureCreator).TemperatureList, (pressureCreator as PressureCreator).PressureList, dbcontext); //Заполененный данными из DB список переменных                
                }
                errorRaisedEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "Get content data from DB" });
                progressBarEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "66" });
            }
            catch (Exception e)
            {
                if (errorRaisedEvent != null)
                    errorRaisedEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "Error in DB Contents" + $" {e.Message }" });
                isiInitSuccess = false;
            }

            //5.5. Формируем группы записи для OPC-сервера
            contentCreator.CreateOPCWriteGroup();
            errorRaisedEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "Create content OPC write group" });
            progressBarEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "68" });

            //5.6. Сообщаемм MainWindow о том, что Contents сформировались и ими можно наполнять ComboBox в ParametersUC
            if (contentsReadyEvent != null)
                contentsReadyEvent.Invoke(this, new EventArgs());

            #endregion

            #region 6. LevelTanks
            //6.1. Пустой список переменных LevelTank (список string с именами тегов)
            //levelTankCreator.CreateItemList();
            levelTankCreator.CreateItemList(sumListLevelsFromDB, NodeElementCollectionFromOPC);
            errorRaisedEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "Create tanks list" });
            progressBarEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "70" });

            //6.2. Формируем группы чтения для OPC-сервера
            levelTankCreator.CreateOPCReadGroup();
            countTanks = levelTankCreator.countItems;
            errorRaisedEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "Create tanks OPC read group" });
            progressBarEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "72" });

            //6.3.Обновленный данными из OPC список переменных LevelTank
            try
            {
                //levelTankCreator.UpdateItemListFromOpc();
                errorRaisedEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "LevelsTank - ok" });
                progressBarEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "74" });
            }
            catch (Exception e)
            {
                if (errorRaisedEvent != null)
                    errorRaisedEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "LevelsTank OPC reading error" + $" {e.Message }" });
                isiInitSuccess = false;
            }


            //6.4. Обновленный данными из DB список переменных LevelTank
            try
            {
                using (DBPGContext dbcontext = new DBPGContext())
                {
                    //(levelTankCreator as LevelTankCreator).UpdateItemListFromDB((levelCreator as LevelCreator).LevelList, (densityCreator as DensityCreator).DensityList, dbcontext); //Заполененный данными из DB список переменных                
                    (levelTankCreator as LevelTankCreatorCitect).UpdateItemListFromDB((levelCreator as LevelCreator).LevelList, (densityCreator as DensityCreatorCitect).DensityList, dbcontext); //Заполененный данными из DB список переменных                
                }
                errorRaisedEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "Get level tanks data from DB" });
                progressBarEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "76" });
            }
            catch (Exception e)
            {
                if (errorRaisedEvent != null)
                    errorRaisedEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "Error in DB Level" + $" {e.Message }" });
                isiInitSuccess = false;
            }

            //6.5. Формируем группы записи для OPC-сервера
           levelTankCreator.CreateOPCWriteGroup();
            errorRaisedEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "Create tanks OPC write group" });
            progressBarEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "78" });

            //6.6. Сообщаемм MainWindow о том, что LevelTanks сформировались и ими можно наполнять ComboBox в ParametersUC
            if (densitiesReadyEvent != null)
                densitiesReadyEvent.Invoke(this, new EventArgs());

            #endregion


            //Сообщаемм MainWindow о том, что все расчетные параметры сформированы
            if (parametersReadyEvent != null)
                parametersReadyEvent.Invoke(this, new EventArgs());

            if (!isiInitSuccess && reinitalizeClientEvent != null)
                reinitalizeClientEvent(this, new CustomEventArgs { ErrorMessage = "Реинициализация при ошибке начального чтения после перезагрузки" });

            //Создаемкласс для вспомогательных вычислений
            //additionalCalculator = new AdditionalCalculator(temperatureCreator, pressureCreator, densityCreator, capacityCreator, contentCreator, singleTagCreator);
            //isiInitSuccess = true;

            errorRaisedEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "All data init success!!!" });
            progressBarEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "100" });
            return isiInitSuccess;
        }


        //Cycle parameters calculating
        internal void CalculateParameters(CancellationToken token)
        {
            int iteratonCount = 0;
            while (!CancellationCommand && !token.IsCancellationRequested)
            {
                timer.Start();

                try
                {
                    temperatureCreator.UpdateItemListFromOpc();
                    pressureCreator.UpdateItemListFromOpc();
                    singleTagCreator.UpdateItemListFromOpc();

                    //Capacity calculation
                    capacityCreator.UpdateItemListFromOpc();
                    foreach (var item in (capacityCreator as CapacityCreatorCitect).CapacityList)
                    {
                        if (!item.IsInValid)
                        {
                            item.CalculateCapacity();
                        }
                    }

                    //Density calculation                    
                    densityCreator.UpdateItemListFromOpc();
                    foreach (var item in (densityCreator as DensityCreatorCitect).DensityList)
                    {
                        if (!item.IsInValid)
                        {
                            item.CalculateDensity();
                        }
                    }

                    //Content calculation     
                    contentCreator.UpdateItemListFromOpc();
                    foreach (var item in (contentCreator as ContentCreatorCitect).ContentList)
                    {
                        if (!item.IsInValid)
                        {
                           item.CalculateContent();
                        }
                    }

                    //LevelTank calculation     
                    //levelTankCreator.UpdateItemListFromOpc();

                    levelCreator.UpdateItemListFromOpc();
                    foreach (var item in (levelTankCreator as LevelTankCreatorCitect).LevelTankList)
                    {
                        if (!item.IsInValid)
                        {
                            item.CalculateTankVolume();
                        }
                    }

                    //Дополнительные расчеты (базируются на ранее расчитанных параметрах)
                    //additionalCalculator.CalculateParameters();


                    //Сообщаем MainWindow о том, что параметры расчитаны успешно
                    if (parameterClaculatedSucceedEvent != null)
                        parameterClaculatedSucceedEvent.Invoke(this, new CustomEventArgs() { IsSucceed = true });

                    AtemtsCount = 0;

                }
                catch (Exception e)
                {
                    //Сообщаем MainWindow о том, что параметры не удалось расчитать
                    if (parameterClaculatedSucceedEvent != null)
                        parameterClaculatedSucceedEvent.Invoke(this, new CustomEventArgs() { IsSucceed = false, ErrorMessage = e.Message });

                    AtemtsCount++;

                    //Запрашиваем реинициализацию подключения, если достигли максимального заданного кол-ва попыток
                    if (AtemtsCount >= maxAttemptsCount && reinitalizeClientEvent != null)
                    {
                        AtemtsCount = 0;
                        reinitalizeClientEvent(this, new CustomEventArgs { ErrorMessage = "Реинициализация в цикле чтения и расчета (5 попыток)" });
                    }

                    IsInstanceActive = false;
                    iteratonCount = 0;
                }

                //Проверяем, изменяется ли бит состояния записи в OPC-сервер на стороне контроллера
                //Если инстанс программы - Primary - записывает Primary
                //Если инстанс программы - Secondary и статус записи Primary не изменяется - записывает Secondary               
                IsInstanceActive = (controllerRole == HostRole.PRIMARY_SERVER && isServerPrimaryFlipping || (controllerRole == HostRole.SECONDARY_SERVER && !isServerPrimaryFlipping && isServerSecondaryFlipping)) && controllerParameters.isEnableWriting;

                //6.7 Записываем singleTag - оба сервера пишут
                switch (controllerRole)
                {
                    case HostRole.PRIMARY_SERVER:
                        (singleTagCreator as SingleTagCreator).IsWritableTagToPLC = (singleTagCreator as SingleTagCreator).SingleTagFromPLC[0] == 0 ? (short)1 : (short)0;
                        break;
                    case HostRole.SECONDARY_SERVER:
                        (singleTagCreator as SingleTagCreator).IsWritableTagToPLC = (singleTagCreator as SingleTagCreator).SingleTagFromPLC[1] == 0 ? (short)1 : (short)0;
                        break;
                }
                try
                {
                    (singleTagCreator as SingleTagCreator).WriteSyncItemToOPC();
                }
                catch (Exception)
                {

                    //Сообщаемм MainWindow о том, что произошла ошибка записи в OPC - сервер
                    if (errorRaisedEvent != null)
                        errorRaisedEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "Can not write value to OPC - Single tag" });
                }

                //Записываем все остальное
                if (IsInstanceActive)
                {
                    try
                    {
                        //3.7 Записываем Capacities в OPC
                        capacityCreator.WriteItemToOPC();

                        //4.7 Записываем Densities в OPC
                        densityCreator.WriteItemToOPC();

                        //5.7 Записываем Contents в OPC
                        contentCreator.WriteItemToOPC();

                        //5.8 Записываем LevelTanks в OPC
                        levelTankCreator.WriteItemToOPC();

                        //5.9 Записываем асинхронные данные для тегов (то, что может писать только один инстанс)
                        //(singleTagCreator as SingleTagCreator).WriteASyncItemToOPC();
                    }
                    catch (Exception)
                    {
                        //Сообщаемм MainWindow о том, что произошла ошибка записи в OPC - сервер
                        if (errorRaisedEvent != null)
                            errorRaisedEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "Can not write value to OPC - all other tags" });
                    }

                    //Записываем результаты расчета в поле Value БД
                    try
                    {
                        using (DBPGContext dbcontext = new DBPGContext())
                        {
                            capacityCreator.WriteItemToDB(dbcontext);
                            densityCreator.WriteItemToDB(dbcontext);
                        }
                    }
                    catch (Exception e)
                    {
                        //Сообщаемм MainWindow о том, что произошла ошибка записи в базу данных
                        if (errorRaisedEvent != null)
                            errorRaisedEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "Can not write value to Data Base" + $" {e.Message }" });
                    }

                }
                //Если контроллер не видит сигнал записи от инстанса программы (SingleTagFromPLC[2] - Primary, SingleTagFromPLC[3] - Secondary) и при этом инстанс продолжает считать данные - запрос на реинициализацию подключения
                if ((
                     (controllerRole == HostRole.PRIMARY_SERVER && isServerPrimaryFlipping && (singleTagCreator as SingleTagCreator).SingleTagFromPLC[3] == 0) ||
                     (controllerRole == HostRole.SECONDARY_SERVER && isServerSecondaryFlipping && (singleTagCreator as SingleTagCreator).SingleTagFromPLC[4] == 0)
                    ) &&
                    reinitalizeClientEvent != null && iteratonCount >= 5 //Даем 5 циклов для сброса аварии записи в контроллере
                   )
                {
                    reinitalizeClientEvent(this, new CustomEventArgs { ErrorMessage = "RESTARTING due to controller writing OPC Alarm!" });
                    break;
                }

                if (iteratonCount <= 5)
                    iteratonCount++;

                //Time mesuring
                timer.Stop();
                timer.Reset();
                Thread.Sleep(pollingPeriod);
            }
        }

        //Таск для проверки, изменяется ли переменная синхронизации в PLC (запущен ли уже другой экземпляр этой программы на другой машине)      

        private void CheckForItemInPlcForWritingToPLC()
        {
            int sum = 0;
            int[] tmpIntArrPrimary = new int[4];
            int[] tmpIntArrSecondary = new int[4];

            int i = 0;

            //isServerPrimaryFlipping = true;
            //isServerSecondaryFlipping = true;


            //Подписываемся на событие считывания и расчета данных из OPC
            parameterClaculatedSucceedEvent += ((o, ea) =>
            {
                if (i >= 4)
                {
                    //Определяем, записывает ли Primary Server свой бит активности в контроллер
                    for (int j = 0; j < tmpIntArrPrimary.Length; j++)
                    {
                        sum += tmpIntArrPrimary[j];
                    }
                    isServerPrimaryFlipping = sum > 0 && sum < 4;
                    i = 0;
                    sum = 0;
                    Array.Clear(tmpIntArrPrimary, 0, tmpIntArrPrimary.Length);

                    //Определяем, записывает ли Secondary Server свой бит активности в контроллер
                    for (int j = 0; j < tmpIntArrSecondary.Length; j++)
                    {
                        sum += tmpIntArrSecondary[j];
                    }
                    isServerSecondaryFlipping = sum > 0 && sum < 4;
                    i = 0;
                    sum = 0;
                    Array.Clear(tmpIntArrSecondary, 0, tmpIntArrSecondary.Length);
                }
                tmpIntArrPrimary[i] = (singleTagCreator as SingleTagCreator).SingleTagFromPLC[0];
                tmpIntArrSecondary[i] = (singleTagCreator as SingleTagCreator).SingleTagFromPLC[1];
                i++;
            });
        }

        // Создаем список со всеми тегами из ОРС
        protected internal void CreateItemListFromOPC()
        {
            var browser = new OpcDaBrowser2(opcServer);
            var items = from s in browser.GetElements(null)
                        select s;
            NodeElementCollectionFromOPC = items.ToList();
        }

        #region Вычитываем все таблицы с БД
        // Методы для вычитывание структур из базы данных
        protected internal void ReadCapacityDataFromDB()
        {
            try
            {
                using (DBPGContext dbcontext = new DBPGContext())
                {
                    var result = from c in dbcontext.capacityDescs
                                     select c;
                    listOfCapacityFromDB = result.ToList();
                }

                foreach (var capacity in listOfCapacityFromDB)
                {
                    sumListCapacityFromDB.Add(capacity.tagname.Trim());
                }
            }
            catch (Exception e)
            {
                if (errorRaisedEvent != null)
                    errorRaisedEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "Error reading Capacity data from DB" + $" {e.Message}" });
            }
        }

        protected internal void ReadDensityDataFromDB()
        {
            try
            {
                using (DBPGContext dbcontext = new DBPGContext())
                {
                    var result = from c in dbcontext.densityDescs
                                 select c;
                    listOfDensityFromDB = result.ToList();
                }
                foreach (var density in listOfDensityFromDB)
                {
                    sumListDensityFromDB.Add(density.tagname.Trim());
                }
            }
            catch (Exception e)
            {
                if (errorRaisedEvent != null)
                    errorRaisedEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "Error reading Density data from DB" + $" {e.Message}" });
            }
        }

        protected internal void ReadContentDataFromDB()
        {
            try
            {
                using (DBPGContext dbcontext = new DBPGContext())
                {
                    var result = from c in dbcontext.contentDescs
                                 select c;
                    listOfContentFromDB = result.ToList();
                }

                foreach (var content in listOfContentFromDB)
                {
                    sumListContentFromDB.Add(content.tagname.Trim());
                }
            }
            catch (Exception e)
            {
                if (errorRaisedEvent != null)
                    errorRaisedEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "Error reading Content data from DB" + $" {e.Message}" });
            }
        }

        protected internal void ReadLevelDataFromDB()
        {
            try
            {
                using (DBPGContext dbcontext = new DBPGContext())
                {
                    var result = from c in dbcontext.tankContents
                                 select c;
                    listOfLevelFromDB = result.ToList();
                }

                foreach (var level in listOfLevelFromDB)
                {
                    if (!String.IsNullOrEmpty(level.tankVarDef))
                        sumListLevelsFromDB.Add(level.tankVarDef.Replace("_TANK", "").Trim());
                }
            }
            catch (Exception e)
            {
                if (errorRaisedEvent != null)
                    errorRaisedEvent.Invoke(this, new CustomEventArgs { ErrorMessage = "Error reading Content data from DB" + $" {e.Message}" });
            }
        }
        // ---------------------------------------------------------------


        // Методы для формирования списка с названия тегов из разных таблиц
        protected internal void GetTemperatureFromDB()
        {
            foreach (var capacity in listOfCapacityFromDB)
            {
                if(!String.IsNullOrEmpty(capacity.temperature))
                    sumListTemperatureFromDB.Add(capacity.temperature.Trim());
            }

            foreach (var content in listOfContentFromDB)
            {
                if (!String.IsNullOrEmpty(content.temperature))
                    sumListTemperatureFromDB.Add(content.temperature.Trim());
            }

            foreach (var density in listOfDensityFromDB)
            {
                if (!String.IsNullOrEmpty(density.temperature))
                    sumListTemperatureFromDB.Add(density.temperature.Trim());
            }
        }

        protected internal void GetPressureFromDB()
        {
            foreach (var capacity in listOfCapacityFromDB)
            {
                if (!String.IsNullOrEmpty(capacity.pressure))
                    sumListPressureFromDB.Add(capacity.pressure.Trim());
            }

            foreach (var content in listOfContentFromDB)
            {
                if (!String.IsNullOrEmpty(content.pressure))
                    sumListPressureFromDB.Add(content.pressure.Trim());
            }

            foreach (var density in listOfDensityFromDB)
            {
                if (!String.IsNullOrEmpty(density.pressure))
                    sumListPressureFromDB.Add(density.pressure.Trim());
            }
        }

        // ---------------------------------------------------------------
        #endregion
    }
}
