using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechParamsCalc.OPC;
using TitaniumAS.Opc.Client.Da;
using TitaniumAS.Opc.Client.Da.Browsing;
using TechParamsCalc.Parameters;

namespace TechParamsCalc.Factory
{
    class TemperatureCreator : ItemsCreator
    {
        private string[] itemDescTemperatureForRead = new string[] { "R" };
        private string[] itemDescTemperatureForWrite = new string[0];

        private OpcDaItemValue[] temperatureValues;
        private int countOfErrorsInReading;

        public List<Temperature> TemperatureList { get; private set; }

        public TemperatureCreator(IOpcClient opcClient) : base(opcClient)
        {
            //@"^.*_T[CT].*$"
            switch (opcClient)
            {
                case OpcClient _:
                    subStringTagName = @"^[S]\d{2,3}[_]\w*_T[CT]\d{2,3}$";
                    break;
                case OpcClientCitect _:
                    subStringTagName = @"^[S]\d{2,3}[_]\d{2,3}[_]\w*_T[CT]\d{2,3}[_]R$";
                    break;
                default:
                    break;
            }
            TemperatureList = new List<Temperature>();
        }

        protected internal override void CreateItemList()
        {
            //Считываем из OPC-Reader строки с названиями переменных

            nodeElementCollection = opcClient.ReadDataToNodeList(subStringTagName).ToList();
            //countItemFromOPC = nodeElementCollection.Count;

            if (opcClient is OpcClientCitect)
            {
                // удаляем _R
                foreach (var item in nodeElementCollection)
                {
                    item.Name = item.Name.Replace("_R", "");
                    item.ItemId = item.Name.Replace("_R", "");
                }
            }
        }

        // перегруженый метод создания списка температур
        protected internal override void CreateItemList(HashSet<string> temperaturesDB,List<OpcDaBrowseElement> opcDaBrowseElements)
        {
            //Считываем из OPC-Reader строки с названиями переменных
            foreach (var temperature in temperaturesDB)
            {
                var temp = opcDaBrowseElements.FirstOrDefault(t => t.ItemId == temperature + "_R");
                if (temp!=null)
                {
                    temp.Name = temp.Name.Replace("_R", "");
                    temp.ItemId = temp.Name.Replace("_R", "");
                    nodeElementCollection.Add(temp);
                }
            }
            countItems = nodeElementCollection.Count;
        }

        protected internal override void CreateOPCReadGroup()
        {
            //Создаем группу для чтения из OPC-сервера
            OpcDaItemResult[] readingResults;
            List<string> listOfValidItems;

            this.InitDataGroup(itemDescTemperatureForRead, "Temperatures_Read_Group", nodeElementCollection, out dataGroupRead, out readingResults, out listOfValidItems);
            listOfValidItems.ForEach(i => TemperatureList.Add(new Temperature(i)));
                
        }

        //Обновляем список Te,perature данными из OPC
        protected internal override void UpdateItemListFromOpc()
        {
            countOfErrorsInReading = 0;
            //System.Windows.Forms.MessageBox.Show("Error with OPC Server reading data. Check Schneider OFS settings (IP address)", "Alert", MessageBoxButtons.OK, MessageBoxIcon.Error);
            temperatureValues = dataGroupRead.Read(dataGroupRead.Items, OpcDaDataSource.Device);

            int _valueCollectionIterator = 0;
            var _temperature = default(Temperature);

            foreach (var item in TemperatureList)
            {
                try
                {
                    //Initialization of fields of temperature instance
                    _temperature = TemperatureList.FirstOrDefault(t => t.TagName == item.TagName);

                    if (_temperature != null && temperatureValues[0 + _valueCollectionIterator].Error.Succeeded)
                    {
                        _temperature.Val_R = (float)temperatureValues[0 + _valueCollectionIterator].Value;
                    }
                    else
                    {
                        _temperature.IsInValid = true;
                        countOfErrorsInReading++;
                    }
                }
                catch (Exception)
                {
                    throw new Exception("Error in Pressures UpdateItemListFromOpc");
                }
                finally
                {
                    _valueCollectionIterator += itemDescTemperatureForRead.Length;
                }

            }

            //if (countOfErrorsInReading > 0)
            //    throw new Exception($"Количество ошибок чтения температур - {countOfErrorsInReading}");
        }
        
    }
}
