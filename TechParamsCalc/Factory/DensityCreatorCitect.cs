using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using TechParamsCalc.DataBaseConnection;
using TechParamsCalc.DataBaseConnection.Density;
using TechParamsCalc.OPC;
using TechParamsCalc.Parameters;
using TitaniumAS.Opc.Client.Da;
using TitaniumAS.Opc.Client.Da.Browsing;

namespace TechParamsCalc.Factory
{
    internal class DensityCreatorCitect : ItemsCreator
    {
        private string[] itemDescDensityForRead = new string[] { "SEL", "VAL_HMI", "VAL_CALC", "DELTA_D", "COMP_N", "PERC_0", "PERC_1", "PERC_2", "PERC_3", "PERC_4" };
        private string[] itemDescDensityForWrite = new string[] { "VAL_CALC" };
        public List<Density> DensityList { get; private set; }
        public event EventHandler densityListGeneratedEvent;                      //Событие - "список переменных сформирован"
        //private short atmoPressure;
        private SingleTagCreator singleTagCreator;
        private OpcDaItemValue[] densityValues;
        private int countOfErrorsInReading;

        public DensityCreatorCitect(IOpcClient opcClient, ItemsCreator itemCreator /*short atmoPressure*/) : base(opcClient)
        {

            subStringTagName = @"^[S]\d{2,3}[_]\d{2,3}[_]\w*[_]DENS_SEL$";

            DensityList = new List<Density>();

            singleTagCreator = itemCreator as SingleTagCreator;
        }

        //Метод для создания пустого списка переменных
        protected internal override void CreateItemList()
        {
            //Считываем из OPC-Reader строки с названиями переменных
            nodeElementCollection = opcClient.ReadDataToNodeList(subStringTagName).ToList();

            // удаляем _R
            foreach (var item in nodeElementCollection)
            {
                item.Name = item.Name.Replace("_SEL", "");
                item.ItemId = item.Name.Replace("_SEL", "");
            }


        }

        // перегруженый метод создания списка density
        protected internal override void CreateItemList(HashSet<string> densityDB, List<OpcDaBrowseElement> opcDaBrowseElements)
        {
            //Считываем из OPC-Reader строки с названиями переменных
            foreach (var density in densityDB)
            {
                var temp = opcDaBrowseElements.FirstOrDefault(t => t.ItemId == density + "_SEL");
                if (temp != null)
                {
                    temp.Name = temp.Name.Replace("_SEL", "");
                    temp.ItemId = temp.Name.Replace("_SEL", "");
                    nodeElementCollection.Add(temp);
                }
            }
            countItems = nodeElementCollection.Count;
        }

        //Создаем группу для чтения из OPC-сервера
        protected internal override void CreateOPCReadGroup()
        {

            //Отправляем в InitDataGroup список всех переменных Dens, т.к. еще не известно, какие из них несуществующие. OpcDaItemResult[] возвращает массив результатов по каждому тегу
            OpcDaItemResult[] readingResults;
            List<string> listOfValidItems;

            this.InitDataGroup(itemDescDensityForRead, "Densities_Read_Group", nodeElementCollection, out dataGroupRead, out readingResults, out listOfValidItems);
            listOfValidItems.ForEach(i => DensityList.Add(new Density(i)));

            if (densityListGeneratedEvent != null)
                densityListGeneratedEvent.Invoke(this, new EventArgs());

        }

        //Создаем группу для записи в OPC-сервер  
        protected internal override void CreateOPCWriteGroup()
        {
            //Принимаем, что список Densities сормировался на этапе формирования группы чтения и не содержит неправильных тегов
            this.InitDataGroup(itemDescDensityForWrite, "Densities_Write_Group", DensityList.Where(c => c.IsWriteble == true), out dataGroupWrite);


            //Инициализация массива объектов для будущей записи в OPC. Отбираются теги, значение "IsWriteble" == false
            this.valuesForWriting = new object[(from e in DensityList.Where(c => c.IsWriteble == true) select e).Count() * itemDescDensityForWrite.Length]; //Массив значений для записи в OPC_сервер
        }


        //Обновление Density тегов из OPC
        protected internal override void UpdateItemListFromOpc()
        {
            densityValues = dataGroupRead.Read(dataGroupRead.Items, OpcDaDataSource.Device);

            countOfErrorsInReading = 0;
            int valueCollectionIterator = 0;
            var density = default(Density);

            foreach (var item in DensityList)
            {
                try
                {
                    //Initialization of fields of density instance
                    density = DensityList.FirstOrDefault(c => c.TagName == item.TagName);

                    if (density != null && densityValues[0 + valueCollectionIterator].Error.Succeeded) // if (density != null)
                    {
                        if (densityValues[0 + valueCollectionIterator].Value != null)
                            density.Sel = (bool)densityValues[0 + valueCollectionIterator].Value;

                        if (densityValues[1 + valueCollectionIterator].Value != null)
                            density.ValHmi = (short)(float.Parse(densityValues[1 + valueCollectionIterator].Value.ToString()) * 10);

                        //_density.val_calc = (short)densityValues[2 + _valueCollectionIterator].Value;

                        if (densityValues[3 + valueCollectionIterator].Value != null)
                            density.DeltaD = (short)(float.Parse(densityValues[3 + valueCollectionIterator].Value.ToString())*100);

                        if (densityValues[4 + valueCollectionIterator].Value != null)
                            density.CompN = short.Parse(densityValues[4 + valueCollectionIterator].Value.ToString());

                        //Инициализация массива description при первом апдейте списка density
                        if (density.PercDescription == null)
                            density.PercDescription = new string[density.CompN];

                        //Инициализация массива значений содержаний компонентов при первом апдейте списка density
                        if (density.PercArray == null)
                            density.PercArray = new double[density.CompN];

                        //Заполнение списка содержданий
                        if (densityValues[5 + valueCollectionIterator].Value != null)
                        {
                            for (int i = 0; i < density.CompN; i++)
                                density.PercArray[i] = double.Parse(densityValues[5 + i + valueCollectionIterator].Value.ToString());
                        }

                        //01.04.2020 Передаем объект SingleTag чтобы прочитать из него атмосферное давление 
                        //density.AtmoPressure = atmoPressure;
                        density.AtmoPressure = singleTagCreator.AtmoPressureFromOPC;
                    }
                    else
                    {
                        density.IsInValid = true;
                        countOfErrorsInReading++;
                    }
                }
                catch (Exception e)
                {
                    throw new Exception($"Density Creator error handling. Tag = {item.TagName} - {e.Message}");
                }
                finally
                {
                    valueCollectionIterator += itemDescDensityForRead.Length;
                }
            }
            //if (countOfErrorsInReading > 0)
            //    throw new Exception($"Количество ошибок чтения Density - {countOfErrorsInReading}");
        }

        //Обновление Capacity тегов из Базы Данных
        protected internal override void UpdateItemListFromDB(List<Temperature> temperatureList, List<Pressure> pressureList, DBPGContext dbContext)
        {
            // считываем описание с базы данных
            var denData = from c in dbContext.densityDescs
                          select c;
            //}
            // -------------------------------------------------------------------------------------------------------
            // обьединяем два листа по условию равности tagName и формируем объект
            var result = from itemDenData in (IEnumerable<DensityContent>)denData
                         join itemDensityList in DensityList on itemDenData.tagname equals itemDensityList.TagName
                         select new
                         {
                             tagname = itemDenData.tagname,
                             percDescription = new string[] {
                                     itemDenData.perc0 ?? string.Empty,
                                     itemDenData.perc1 ?? string.Empty,
                                     itemDenData.perc2 ?? string.Empty,
                                     itemDenData.perc3 ?? string.Empty,
                                     itemDenData.perc4 ?? string.Empty,
                                 },

                             temperature = temperatureList.FirstOrDefault(x => x.TagName == itemDenData.temperature),
                             pressure = pressureList.FirstOrDefault(x => x.TagName == itemDenData.pressure),
                             description = itemDenData.description,
                             isWriteble = itemDenData.isWritable
                         };

            // ----------------------------------------------------------------------------
            var density = default(Characteristic);
            foreach (var item in result)
            {
                density = DensityList.FirstOrDefault(c => c.TagName == item.tagname);

                try
                {
                    if (density != null)
                    {
                        Array.Copy(item.percDescription, ((Density)density).PercDescription, ((Density)density).CompN);
                        //Array.Copy(item.percDescription, ((Density)density).PercDescription, item.percDescription.Length);
                        density.Description = item.description;
                        ((Density)density).Temperature = item.temperature as Temperature;
                        ((Density)density).Pressure = item.pressure as Pressure == null ? new Pressure("PressureSample") : item.pressure as Pressure;
                        ((Density)density).IsWriteble = item.isWriteble ?? false;
                    }
                }
                catch (Exception e)
                {
                    MessageBox.Show($"{density.TagName}\n{e.Message}", "Alert!", MessageBoxButton.OK, MessageBoxImage.Error);
                    density.IsInValid = true;
                }
            }
        }

        //Запись тегов в OPC
        protected internal override void WriteItemToOPC()
        {
            int i = 0;
            foreach (var item in DensityList)
            {
                if (item.IsWriteble)
                    valuesForWriting[i++] = item.ValCalc;
            }

            if (opcClient.OpcServer.IsConnected)
                opcClient.WriteMultiplyItems(dataGroupWrite, valuesForWriting);
        }

        //Запись тегов в БД
        protected internal override void WriteItemToDB(DBPGContext context)
        {
            foreach (var item in DensityList)
            {
                try
                {
                    context.densityDescs.FirstOrDefault(c => c.tagname == item.TagName).value = item.ValCalc;
                }
                catch (Exception)
                {
                    throw new Exception($"Tag = {item.TagName}");
                }

            }
            context.SaveChanges();
        }
    }
}
