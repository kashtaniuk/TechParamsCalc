using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using TechParamsCalc.OPC;
using TitaniumAS.Opc.Client.Da;
using TitaniumAS.Opc.Client.Da.Browsing;

namespace TechParamsCalc.Factory
{
    internal class SingleTagCreator : ItemsCreator
    {



        #region Пишем в OPC

        public short IsWritableTagToPLC { get; set; }           //Слово, которое сигнализирует о том, что в OPC идет запись
        public short PropyleneMass { get; set; }                //Расчетная масса пропилена для расчта задания расхода на Т02
        public short DeltaPE06 { get; set; }                    //Расчетный перепад давления в 1.E06 после теплообменника 1.E23
        public short DeltaPR01 { get; set; }                    //Расчетная дельта к заданию давления реакции(см.AdditionalCalculator класс)
        public short PeroxideMixRatio { get; set; }             //Расчетное соотношение перекиси к реакционной смеси 1 для подержания точки азиотропы
        public short AcnStrength { get; set; }                  //Расчетная крепость ACN в колоне 1.Т01 по расходу 100% перекиси, вычисленной по заданному расходу перекиси на реакторы

        public short PoStrengthT03_T06_87PercAcn { get; set; }  //Расчетная крепость PO от колонны 1.Т03 к 1.T06 (итеративный расчет) при 87% содержания ACN в смеси ACN-Water
        public short PoStrengthT03_T06_0PercAcn { get; set; }   //Расчетная крепость PO от колонны 1.Т03 к 1.T06 (итеративный расчет) при 0% содержания ACN в смеси ACN-Water

        public short PoStrengthP03_87PercAcn { get; set; }      //Расчетная крепость PO к сборнику со склада в колонну 1.Т01 (итеративный расчет) при 87% содержания ACN в смеси ACN-Water
        public short PoStrengthP03_0PercAcn { get; set; }       //Расчетная крепость PO к сборнику со склада в колонну 1.Т01 (итеративный расчет) при 0% содержания ACN в смеси ACN-Water

        public short PoStrengthT06_D08_87PercAcn { get; set; }  //Расчетная крепость PO от колонны 1.Т06 к сборнику 1.D08 (итеративный расчет) при 87% содержания ACN в смеси ACN-Water
        public short PoStrengthT06_D08_0PercAcn { get; set; }   //Расчетная крепость PO от колонны 1.Т06 к сборнику 1.D08 (итеративный расчет) при 0% содержания ACN в смеси ACN-Water


        public short[] S11_P13_2_FT01_PERC { get; set; }        //Расчетные проценты массового содержания компонентов в смеси после насоса 1.P13 в сборник 1.D08

        public short[] S13_P03_FT01_PERC { get; set; }          //Расчетные проценты массового содержания компонентов в смеси S13_P03_FT01


        public short[] S11_T06_FT02_PERC { get; set; }           //Расчетные проценты массового содержания компонентов в смеси S11_T06_FT01

        #endregion


        #region Читаем из OPC

        //Список имен переменных для чтения
        public string[] SingleTagNames { get; private set; }

        public short[] SingleTagFromPLC { get; private set; }   //Теги обмена данными по сети от контроллера. Слово, которое сигнализирует о том, что в OPC идет запись располагается в SingleTagFromPLC[0]
        public short AtmoPressureFromOPC { get; set; } = 10000/*10101*/; //Тег атмосферного давления от контроллера        

        public float averReactFlow { get; set; }    //Расход реакционной смеси от А01 для расчета расхода пропилена на А02
        public float AcnWaterMassFlow { get; set; } //РАсхо массовый ACN с водой из D02
        public float S11_R01_TT01_SP { get; set; } //Задание температуры в реакторе 1.R01
        public float S11_P05_FC01_HMI { get; set; } //Массовый расход рнакционно смеси 1 после Р05
        public float S12_P02_AP01_HMI { get; set; } //% содержания перекиси водорода со склада        
        public float S11_A01_FC02_AVER_HMI { get; set; }//Расход массовый ACN из сборника 1.D02 
        public float S11_D02_AP01_HMI { get; set; } //Содержание ACN в сборнике 1.D02
        public float S12_P02_FT01_SP { get; set; } //Заданный массовый расход перекиси к реакторам
        public float S11_T01_PT05_AZEO_HMI { get; set; } //Крепость ACN в колонне 1.Т01 в точке азеотропы
        public float S11_P13_FT01_Mass_TEMPERATURE { get; set; } //Температура от массового расходомера PO в сборник 1.D08
        public float S11_P13_FT01_Mass_DENSITY { get; set; } //Плотность от массового расходомера PO в сборник 1.D08
        public float S13_P03_FT01_Mass_DENSITY { get; set; } //Плотность от массового расходомера PO S13_P03_FC01
        public float S13_P03_FT01_Mass_TEMPERATURE { get; set; } //Температура от массового расходомера PO со склада
        public float S11_T06_FT02_Mass_DENSITY { get; set; } //Плотность от массового расходомера PO в колонну 1.T06

        public float S11_T06_FT02_Mass_TEMPERATURE { get; set; } //Температура от массового расходомера PO в колонну 1.T06

        public double S11_T06_AP01_START_WATER { get; set; } //% воды в смеси от колонны 1.Т06 к сборнику 1.D08
        public double S11_T06_AP01_START_ALD { get; set; } //% альдегидов в смеси от колонны 1.Т06 к сборнику 1.D08
        #endregion

        //группа для записи переменных, которые должны писаться одновременно двумя серверами
        private OpcDaGroup dataGroupForSynchroWriting;
        private object[] syncValuesForWriting;

        public SingleTagCreator(IOpcClient opcClient, string[] singleTagNames) : base(opcClient)
        {
            SingleTagNames = singleTagNames;
            SingleTagFromPLC = new short[100];

        }

        protected internal override void CreateItemList()
        {
            throw new NotImplementedException();
        }


        protected internal override void CreateOPCReadGroup()
        {
            //Создаем группу для чтения из OPC-сервера
            var simpleTagsItemDefinitions = new OpcDaItemDefinition[]
            {
                //[0] Чтение массива переменных Exchange из PLC (элемент 0 массива - статус записи Primary Sever, элемент 1 - для Secondary Server'a)
                new OpcDaItemDefinition
                {
                    ItemId = opcClient.ParentNodeDescriptor + "EXTERNAL_EXCHANGE_0", // SingleTagNames[0],
                    IsActive = true
                },

                //[1] Атмосферное давление
                new OpcDaItemDefinition
                {
                    ItemId = opcClient.ParentNodeDescriptor + "EXTERNAL_EXCHANGE_1", // SingleTagNames[1],
                    IsActive = true
                },

                //[2] Расход реакционной смеси от А01 для расчета расхода пропилена на А02 
                new OpcDaItemDefinition
                {
                    ItemId = opcClient.ParentNodeDescriptor + "EXTERNAL_EXCHANGE_2", // S11_P05_FC08_AVER_HMI
                    IsActive = true
                },

                //[3] РАсход массовый ACN с водой из D02
                new OpcDaItemDefinition
                {
                    ItemId = opcClient.ParentNodeDescriptor + "EXTERNAL_EXCHANGE_3", // S11_A01_FC02_AVER_HMI
                    IsActive = true
                },

                //[4] Задание температуры в реакторе 1.R01 для расчета дельты к заданию давления реакции (см. AdditionalCalculator класс)
                new OpcDaItemDefinition
                {
                    ItemId = opcClient.ParentNodeDescriptor + "EXTERNAL_EXCHANGE_4", // S11_R01_TT01_SP
                    IsActive = true
                }

                //[5] Массовый расход реакционной смеси S11_P05_FC01_HMI
                //new OpcDaItemDefinition
                //{
                //    ItemId = opcClient.ParentNodeDescriptor + "S11_P05_FC01.HMI",
                //    IsActive = true
                //},

                //[6] Плотность перекиси со склада (лабораторные показатели)
                //new OpcDaItemDefinition
                //{
                //    ItemId = opcClient.ParentNodeDescriptor + "S12_P02_AP01.HMI",
                //    IsActive = true
                //},

                //[7] Расход массовый ACN из сборника 1.D02 
                //new OpcDaItemDefinition
                //{
                //    ItemId = opcClient.ParentNodeDescriptor + "S11_A01_FC02_AVER_HMI",
                //    IsActive = true
                //},

                //[8] Содержание ACN в сборнике 1.D02
                //new OpcDaItemDefinition
                //{
                //    ItemId = opcClient.ParentNodeDescriptor + "S11_A01_FC02_DENS.PERC[1]",
                //    IsActive = true
                //},

                //[9] Заданный массовый расход перекиси к реакторам
                //new OpcDaItemDefinition
                //{
                //    ItemId = opcClient.ParentNodeDescriptor + "S12_P02_FT01_SP",
                //    IsActive = true
                //},

                //[10] Крепость ACN в колонне 1.Т01 в точке азеотропы
                //new OpcDaItemDefinition
                //{
                //    ItemId = opcClient.ParentNodeDescriptor + "S11_T01_PT05_AZEO_HMI",
                //    IsActive = true
                //},                
                
                //[11] Температура от массового расходомера PO в сборник 1.D08
                //new OpcDaItemDefinition
                //{
                //    ItemId = opcClient.ParentNodeDescriptor + "S11_P13_FT01_Mass.TEMPERATURE",
                //    IsActive = true
                //},

                //[12] Плотность от массового расходомера PO в сборник 1.D08
                //new OpcDaItemDefinition
                //{
                //    ItemId = opcClient.ParentNodeDescriptor + "S11_P13_FT01_Mass.DENSITY",
                //    IsActive = true
                //},

                //[13] Плотность от массового расходомера PO S13_P03_FC01
                //new OpcDaItemDefinition
                //{
                //    ItemId = opcClient.ParentNodeDescriptor + "S13_P03_FT01_Mass.DENSITY",
                //    IsActive = true
                //},

                //[14] Температура от массового расходомера PO со склада
                //new OpcDaItemDefinition
                //{
                //    ItemId = opcClient.ParentNodeDescriptor + "S13_P03_FT01_Mass.TEMPERATURE",
                //    IsActive = true
                //},

                //[15] Плотность от массового расходомера PO в колонну 1.T06 S11_T06_FT02
                //new OpcDaItemDefinition
                //{
                //    ItemId = opcClient.ParentNodeDescriptor + "S11_T06_FT02_Mass.DENSITY",
                //    IsActive = true
                //},

                //[16] Температура от массового расходомера PO в колонну 1.T06 S11_T06_FT02
                //new OpcDaItemDefinition
                //{
                //    ItemId = opcClient.ParentNodeDescriptor + "S11_T06_FT02_Mass.TEMPERATURE",
                //    IsActive = true
                //},

                //[17] % воды в смеси от колонны 1.Т06 к сборнику 1.D08
                //new OpcDaItemDefinition
                //{
                //    ItemId = opcClient.ParentNodeDescriptor + "S11_T06_AP01_START_WATER",
                //    IsActive = true
                //},

                //[18] % альдегидов в смеси от колонны 1.Т06 к сборнику 1.D08
                //new OpcDaItemDefinition
                //{
                //    ItemId = opcClient.ParentNodeDescriptor + "S11_T06_AP01_START_ALD",
                //    IsActive = true
                //}


            };

            dataGroupRead = opcClient.OpcServer.AddGroup("SingleTagGroupRead");                               //Группа переменных для чтения (записи) из OPC-сервера 
            dataGroupRead.IsActive = true;
            OpcDaItemResult[] results = dataGroupRead.AddItems(simpleTagsItemDefinitions);                   //Добавление переменных в группу             
            countItems = results.Length;
        }

        protected internal override void UpdateItemListFromOpc()
        {
            OpcDaItemValue[] singleValues;
            try
            {
                singleValues = dataGroupRead.Read(dataGroupRead.Items, OpcDaDataSource.Device);
            }
            catch (Exception e)
            {
                throw new Exception("Error in group creating for single tags!" + e.Message);
            }

            #region Переприсвоение тегов из массива items, прочитанных из OPC

            try
            {
                //[0] Массив short'ов [0-100]
                if (singleValues[0].Error.Succeeded)
                    SingleTagFromPLC[0] = short.Parse(singleValues[0].Value.ToString());

                //[1] Тег атмосферного давления от контроллера        

                if (singleValues[1].Error.Succeeded)
                    SingleTagFromPLC[1] = short.Parse(singleValues[1].Value.ToString());
                //AtmoPressureFromOPC = (short)singleValues[1].Value;

                //[2] Усредненный объемный расход реакционной смеси    

                //if (singleValues[2].Error.Succeeded)
                //    averReactFlow = (short)singleValues[2].Value * 0.1f;

                //[3] Усредненный массовый расход ACN и воды из A02

                if (singleValues[3].Error.Succeeded)
                    SingleTagFromPLC[3] = short.Parse(singleValues[3].Value.ToString());
                //AcnWaterMassFlow = (short)(singleValues[3].Value) * 0.1f;

                //[4] Задание температуры в реакторе 1.R01 для расчета дельты к заданию давления реакции (см. AdditionalCalculator класс)

                if (singleValues[4].Error.Succeeded)
                    SingleTagFromPLC[4] = short.Parse(singleValues[4].Value.ToString());
                //S11_R01_TT01_SP = (short)(singleValues[4].Value) * 0.1f;

                //[5] Массовый расход реакционной смеси S11_P05_FC01_HMI
                //if (singleValues[5].Error.Succeeded)
                //    S11_P05_FC01_HMI = (short)(singleValues[5].Value) * 0.1f;

                //[6] Плотность перекиси со склада (лабораторные показатели)
                //if (singleValues[6].Error.Succeeded)
                //    S12_P02_AP01_HMI = (short)(singleValues[6].Value) * 0.01f;

                //[7] Расход массовый ACN из сборника 1.D02 
                //if (singleValues[7].Error.Succeeded)
                //    S11_A01_FC02_AVER_HMI = (short)(singleValues[7].Value) * 0.1f;

                //[8] Содержание ACN в сборнике 1.D02
                //if (singleValues[8].Error.Succeeded)
                //    S11_D02_AP01_HMI = (short)(singleValues[8].Value) * 0.01f;

                //[9] Заданный массовый расход перекиси к реакторам
                //if (singleValues[9].Error.Succeeded)
                //    S12_P02_FT01_SP = (short)(singleValues[9].Value) * 0.1f;

                //[10] Крепость ACN в колонне 1.Т01 в точке азеотропы
                //if (singleValues[10].Error.Succeeded)
                //    S11_T01_PT05_AZEO_HMI = (short)(singleValues[10].Value) * 0.01f;

                //[11] Температура от массового расходомера PO в сборник 1.D08
                //if (singleValues[11].Error.Succeeded)
                //    S11_P13_FT01_Mass_TEMPERATURE = (float)singleValues[11].Value;

                //[12] Плотность от массового расходомера PO в сборник 1.D08
                //if (singleValues[12].Error.Succeeded)
                //    S11_P13_FT01_Mass_DENSITY = (float)singleValues[12].Value;

                //[13] Плотность от массового расходомера PO S13_P03_FC01
                //if (singleValues[13].Error.Succeeded)
                //    S13_P03_FT01_Mass_DENSITY = (float)singleValues[13].Value;

                //[14] Температура от массового расходомера PO со склада
                //if (singleValues[14].Error.Succeeded)
                //    S13_P03_FT01_Mass_TEMPERATURE = (float)singleValues[14].Value;

                //[15] Плотность от массового расходомера PO в колонну 1.T06 S11_T06_FT02
                //if (singleValues[15].Error.Succeeded)
                //    S11_T06_FT02_Mass_DENSITY = (float)singleValues[15].Value;

                //[16] Температура от массового расходомера PO в колонну 1.T06 S11_T06_FT02
                //if (singleValues[16].Error.Succeeded)
                //    S11_T06_FT02_Mass_TEMPERATURE = (float)singleValues[16].Value;

                //[17] % воды в смеси от колонны 1.Т06 к сборнику 1.D08
                //if (singleValues[17].Error.Succeeded)
                //    S11_T06_AP01_START_WATER = Convert.ToDouble(singleValues[17].Value);

                //[18] % альдегидов в смеси от колонны 1.Т06 к сборнику 1.D08
                //if (singleValues[18].Error.Succeeded)
                //    S11_T06_AP01_START_ALD = Convert.ToDouble(singleValues[18].Value);

                #endregion
            }
            catch (Exception)
            {


            }
        }


        protected internal void CreateOPCWriteGroup(bool isPrimaryServer)
        {
            OpcDaItemResult[] results;

            //Создаем группу для записи из OPC-сервера

            //---Группа 1 (синхронная запись)---------------------//

            dataGroupForSynchroWriting = opcClient.OpcServer.AddGroup("SingleTagSynchroGroupWrite");         //Группа переменных для одновременной записи в  OPC 
            dataGroupForSynchroWriting.IsActive = true;

            var syncWriteItems = new OpcDaItemDefinition[]
            {
                new OpcDaItemDefinition
                {
                    //ItemId = opcClient.ParentNodeDescriptor + SingleTagNames[0] + (isPrimaryServer ? "[0]" : "[1]"),
                    ItemId = opcClient.ParentNodeDescriptor + SingleTagNames[0] + (isPrimaryServer ? "_0" : "_1"),
                    IsActive = true
                }
        };

            results = dataGroupForSynchroWriting.AddItems(syncWriteItems);
            syncValuesForWriting = new object[results.Count()]; //Массив значений для записи в OPC_сервер


            //---Группа 2 (Асинхронная запись)---------------------//

            dataGroupWrite = opcClient.OpcServer.AddGroup("SingleTagAsynchroGroupWrite");          //Группа переменных для разразненной записи в  OPC  
            dataGroupWrite.IsActive = true;

            var aSyncWriteItems = new OpcDaItemDefinition[]
            {
                //[0] Переменая "Масса пропилена" для задания расхода пропилена на T02
                new OpcDaItemDefinition
                {
                    ItemId = opcClient.ParentNodeDescriptor + SingleTagNames[0] + "[20]",
                    IsActive = true
                },

                //[1] Расчетный перепад давления в 1.E06 после теплообменника 1.E23
                new OpcDaItemDefinition
                {
                    ItemId = opcClient.ParentNodeDescriptor + SingleTagNames[0] + "[21]",
                    IsActive = true
                },

                //[2] Расчетная дельта к заданию давления реакции (см. AdditionalCalculator класс)
                new OpcDaItemDefinition
                {
                    ItemId = opcClient.ParentNodeDescriptor + SingleTagNames[0] + "[22]",
                    IsActive = true
                },

                //[3] Расчетная крепость ACN в колонне 1.Т01 для поддержания точки азиотропы
                new OpcDaItemDefinition
                {
                    ItemId = opcClient.ParentNodeDescriptor + SingleTagNames[0] + "[23]",
                    IsActive = true
                },

                //[4] Расчетная крепость ACN в колоне 1.Т01 по расходу 100% перекиси, вычисленной по заданному расходу перекиси на реакторы
                new OpcDaItemDefinition
                {
                    ItemId = opcClient.ParentNodeDescriptor + SingleTagNames[0] + "[24]",
                    IsActive = true
                },

                //[5] Расчетная крепость PO от колонны 1.Т03 к 1.Т06 (итеративный расчет) при 87% ACN в паре ACN-Water
                new OpcDaItemDefinition
                {
                    ItemId = opcClient.ParentNodeDescriptor + SingleTagNames[0] + "[25]",
                    IsActive = true
                },

                //[6] Расчетные проценты массового содержания компонентов в смеси от колонны 1.Т03 в 1.Т06
                new OpcDaItemDefinition
                {
                    ItemId = opcClient.ParentNodeDescriptor + "S11_T03_AP03_DENS.PERC",
                    IsActive = true
                },

                //[7] Расчетная крепость PO к сборнику со склада в колонну 1.Т01 (итеративный расчет) при 87% ACN в паре ACN-Water
                new OpcDaItemDefinition
                {
                    ItemId = opcClient.ParentNodeDescriptor + SingleTagNames[0] + "[26]",
                    IsActive = true
                },

                //[8] Расчетные проценты массового содержания компонентов в смеси S13_P03_FT01
                new OpcDaItemDefinition
                {
                    ItemId = opcClient.ParentNodeDescriptor + "S13_P03_QC01_DENS.PERC",
                    IsActive = true
                },
                
                //[9] Расчетная крепость PO от колонны 1.Т03 к 1.Т06 (итеративный расчет) при 0% ACN в паре ACN-Water
                new OpcDaItemDefinition
                {
                    ItemId = opcClient.ParentNodeDescriptor + SingleTagNames[0] + "[27]",
                    IsActive = true
                },

                //[10] Расчетная крепость PO к сборнику со склада в колонну 1.Т01 (итеративный расчет) при 0% ACN в паре ACN-Water
                new OpcDaItemDefinition
                {
                    ItemId = opcClient.ParentNodeDescriptor + SingleTagNames[0] + "[28]",
                    IsActive = true
                },

                //[11] Расчетная крепость PO от колонны 1.Т06 к сборнику 1.D08 (итеративный расчет) при 87% ACN в паре ACN-Water
                new OpcDaItemDefinition
                {
                    ItemId = opcClient.ParentNodeDescriptor + SingleTagNames[0] + "[29]",
                    IsActive = true
                },

                //[12] Расчетная крепость PO от колонны 1.Т06 к сборнику 1.D08 (итеративный расчет) при 0% ACN в паре ACN-Water
                new OpcDaItemDefinition
                {
                    ItemId = opcClient.ParentNodeDescriptor + SingleTagNames[0] + "[30]",
                    IsActive = true
                },

                //[13] Расчетные проценты массового содержания компонентов в смеси от колонны 1.Т06 к сборнику 1.D08
                new OpcDaItemDefinition
                {
                    ItemId = opcClient.ParentNodeDescriptor + "S11_D08_AP01_DENS.PERC",
                    IsActive = true
                }
        };

            results = dataGroupWrite.AddItems(aSyncWriteItems);
            valuesForWriting = new object[results.Count()];
        }


        //Запись синхронной группы
        protected internal void WriteSyncItemToOPC()
        {
            //Первое значение синхронной группы - всегда бит активности сервера
            syncValuesForWriting[0] = IsWritableTagToPLC;

            if (opcClient.OpcServer.IsConnected)
                opcClient.WriteMultiplyItems(dataGroupForSynchroWriting, syncValuesForWriting);
        }

        //Запись асинхронной группы
        protected internal void WriteASyncItemToOPC()
        {
            valuesForWriting[0] = PropyleneMass;
            valuesForWriting[1] = DeltaPE06;
            valuesForWriting[2] = DeltaPR01;
            valuesForWriting[3] = PeroxideMixRatio;
            valuesForWriting[4] = AcnStrength;
            valuesForWriting[5] = PoStrengthT03_T06_87PercAcn;
            valuesForWriting[6] = S11_T06_FT02_PERC;
            valuesForWriting[7] = PoStrengthP03_87PercAcn;
            valuesForWriting[8] = S13_P03_FT01_PERC;
            valuesForWriting[9] = PoStrengthT03_T06_0PercAcn;
            valuesForWriting[10] = PoStrengthP03_0PercAcn;
            valuesForWriting[11] = PoStrengthT06_D08_0PercAcn;
            valuesForWriting[12] = PoStrengthT06_D08_87PercAcn;
            valuesForWriting[13] = S11_P13_2_FT01_PERC;

            //........Добавить при необходимости

            if (opcClient.OpcServer.IsConnected)
                opcClient.WriteMultiplyItems(dataGroupWrite, valuesForWriting);
        }

    }
}
