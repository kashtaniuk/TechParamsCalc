using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using TechParamsCalc.OPC;
using TitaniumAS.Opc.Client.Da;
using TitaniumAS.Opc.Client.Da.Browsing;
using TechParamsCalc.Parameters;
using TechParamsCalc.DataBaseConnection;
using TechParamsCalc.DataBaseConnection.Content;

namespace TechParamsCalc.Factory
{
    internal class ContentCreatorCitect : ItemsCreator
    {
        private string[] itemDescContentForRead = new string[] { "SEL", "VAL_HMI", "VAL_CALC_0", "VAL_CALC_1", "VAL_CALC_2", "VAL_CALC_3", "VAL_CALC_4", "DELTA_P_0", "DELTA_P_1", "DELTA_P_2", "DELTA_P_3", "DELTA_P_4", "DELTA_T_0", "DELTA_T_1", "DELTA_T_2", "DELTA_T_3", "DELTA_T_4", "CONF"};
        private string[] itemDescContentForWrite = new string[] { "VAL_CALC_0", "VAL_CALC_1", "VAL_CALC_2", "VAL_CALC_3", "VAL_CALC_4" };
        public List<Content> ContentList { get; private set; }
        public event EventHandler contentListGeneratedEvent;                      //Событие - "список переменных сформирован"
        private SingleTagCreator singleTagCreator;
        private OpcDaItemValue[] contentValues;
        private int countOfErrorsInReading;

        public ContentCreatorCitect(IOpcClient opcClient, ItemsCreator itemCreator/*short atmoPressure*/) : base(opcClient)
        {          
            subStringTagName = @"^[S]\d{2,3}[_]\d{2,3}[_]\w*[_]CONT_SEL$";

            ContentList = new List<Content>();

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

        // перегруженый метод создания списка content
        protected internal override void CreateItemList(HashSet<string> contentDB, List<OpcDaBrowseElement> opcDaBrowseElements)
        {
            //Считываем из OPC-Reader строки с названиями переменных
            foreach (var content in contentDB)
            {
                var temp = opcDaBrowseElements.FirstOrDefault(t => t.ItemId == content + "_SEL");
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
            //Отправляем в InitDataGroup список всех переменных Conts, т.к. еще не известно, какие из них несуществующие. OpcDaItemResult[] возвращает массив результатов по каждому тегу
            OpcDaItemResult[] readingResults;
            List<string> listOfValidItems;

            this.InitDataGroup(itemDescContentForRead, "Contents_Read_Group", nodeElementCollection, out dataGroupRead, out readingResults, out listOfValidItems);
            listOfValidItems.ForEach(i => ContentList.Add(new Content(i)));

            //string contentName;
            //Формируем список пустых объектов Density
            //foreach (var result in readingResults)
            //{
            //    if (result.Error.Succeeded)
            //    {
            //        contentName = result.Item.ItemId.Substring(0, result.Item.ItemId.IndexOf("_TANK") + 1);
            //        //contentName = contentName.Substring(0, contentName.LastIndexOf('.'));
            //        if (!ContentList.Any(d => d.TagName == contentName))
            //            ContentList.Add(new Content(contentName));
            //    }
            //}

            if (contentListGeneratedEvent != null)
                contentListGeneratedEvent.Invoke(this, new EventArgs());
        }


        //Создаем группу для записи в OPC-сервер   
        protected internal override void CreateOPCWriteGroup()
        {
            //Принимаем, что список Contents сформировался на этапе формирования группы чтения и не содержит неправильных тегов            
            this.InitDataGroup(itemDescContentForWrite, "Contents_Write_Group", ContentList.Where(c => c.IsWriteble == true), out dataGroupWrite);

            //Инициализация массива объектов для будущей записи в OPC. Отбираются теги, значение "IsWriteble" == false
            this.valuesForWriting = new object[(from e in ContentList.Where(c => c.IsWriteble == true) select e).Count() * itemDescContentForWrite.Length]; //Массив значений для записи в OPC_сервер
        }


        //Обновление Capacity тегов из OPC
        protected internal override void UpdateItemListFromOpc()
        {
            contentValues = dataGroupRead.Read(dataGroupRead.Items, OpcDaDataSource.Device);

            countOfErrorsInReading = 0;
            int valueCollectionIterator = 0;
            var content = default(Content);

            foreach (var item in ContentList) // nodeElementCollection
            {
                try
                {
                    //Initialization of fields of capacity instance
                    content = ContentList.FirstOrDefault(c => c.TagName == item.TagName);

                    if (content != null && contentValues[0 + valueCollectionIterator].Error.Succeeded)
                    {

                        if (contentValues[0 + valueCollectionIterator].Value != null)
                            content.Sel = short.Parse(contentValues[0 + valueCollectionIterator].Value.ToString());

                        if (contentValues[1 + valueCollectionIterator].Value != null)
                            content.ValHmi = (short)(double.Parse(contentValues[1 + valueCollectionIterator].Value.ToString()) * 100);

                        //_capacity.val_calc = (short)capacityValues[2 + _valueCollectionIterator].Value;

                        for (int i = 0; i < content.DeltaP.Length; i++)
                            content.DeltaP[i] = (short)(double.Parse(contentValues[7 + i + valueCollectionIterator].Value.ToString()) * 100);

                        for (int i = 0; i < content.DeltaT.Length; i++)
                            content.DeltaT[i] = (short)(double.Parse(contentValues[12 + i + valueCollectionIterator].Value.ToString()) * 10);

                        if (contentValues[17 + valueCollectionIterator].Value != null)
                            content.Conf = short.Parse(contentValues[17 + valueCollectionIterator].Value.ToString());

                        //Инициализация массива description при первом апдейте списка capacity
                        if (content.PercDescription == null)
                            content.PercDescription = new string[5];

                        content.AtmoPressure = singleTagCreator.AtmoPressureFromOPC;
                    }
                    else
                    {
                        content.IsInValid = true;
                        countOfErrorsInReading++;
                    }
                }
                catch (Exception)
                {
                    throw new Exception($"Content Creator error handling. Tag = {item.TagName}");
                }
                finally
                {
                    valueCollectionIterator += itemDescContentForRead.Length;
                }
            }
            //if (countOfErrorsInReading > 0)
            //    throw new Exception($"Количество ошибок чтения Content - {countOfErrorsInReading}");
        }

        //Обновление Content тегов из Базы Данных
        protected internal override void UpdateItemListFromDB(List<Temperature> temperatureList, List<Pressure> pressureList, DBPGContext dbContext)
        {
            // считываем описание с базы данных
            var contentData = from c in dbContext.contentDescs
                              select c;
            //}
            // -------------------------------------------------------------------------------------------------------
            // обьединяем два листа по условию равности tagName и формируем объект
            var result = from itemContData in (IEnumerable<ContentContent>)contentData
                         join itemContentList in ContentList on itemContData.tagname equals itemContentList.TagName
                         select new
                         {
                             tagname = itemContData.tagname,
                             percDescription = new string[] {
                                     itemContData.comp0 ?? string.Empty,
                                     itemContData.comp1 ?? string.Empty,
                                     itemContData.comp2 ?? string.Empty,
                                     itemContData.comp3 ?? string.Empty,
                                     itemContData.comp4 ?? string.Empty,
                                 },

                             temperature = temperatureList.FirstOrDefault(x => x.TagName == itemContData.temperature),
                             pressure = pressureList.FirstOrDefault(x => x.TagName == itemContData.pressure),
                             description = itemContData.description,
                             isWriteble = itemContData.isWritable
                         };


            // ----------------------------------------------------------------------------
            var content = default(Characteristic);
            foreach (var item in result)
            {
                content = ContentList.FirstOrDefault(c => c.TagName == item.tagname);

                try
                {
                    if (content != null)
                    {
                        Array.Copy(item.percDescription, ((Content)content).PercDescription, 5); //5 компонентов

                        //for (int i = 0; i < item.percDescription.Length; i++)
                        //{
                        //    if (item.percDescription[i] != null)
                        //    {
                        //        ((Content)content).PercDescription[i] = item.percDescription[i];
                        //    }
                        //}

                        content.Description = item.description;
                        ((Content)content).Temperature = item.temperature as Temperature;
                        ((Content)content).Pressure = item.pressure as Pressure == null ? new Pressure("PressureSample") : item.pressure as Pressure;
                        ((Content)content).IsWriteble = item.isWriteble ?? false;
                    }
                }
                catch (Exception e)
                {
                    MessageBox.Show(e.Message, "Alert!", MessageBoxButton.OK, MessageBoxImage.Error);
                    content.IsInValid = true;
                }
            }
        }


        //Запись тегов в OPC
        protected internal override void WriteItemToOPC()
        {
            int i = 0;
            foreach (var item in ContentList)
            {
                if (item.IsWriteble)
                {
                    foreach (short value in item.ValCalc)
                    {
                        valuesForWriting[i++] = value;
                    }
                }
                //valuesForWriting[i++] = item.ValCalc;
            }

            if (opcClient.OpcServer.IsConnected)
                opcClient.WriteMultiplyItems(dataGroupWrite, valuesForWriting);
        }




    }
}
