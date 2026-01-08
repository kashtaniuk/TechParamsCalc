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
    class PressureCreator : ItemsCreator
    {
        private string[] itemDescPressureForRead = new string[] { "R" };
        private string[] itemDescPressureForWrite = new string[0];
        private int countOfErrorsInReading;

        private OpcDaItemValue[] pressureValues;
        public List<Pressure> PressureList { get; private set; }

        public PressureCreator(IOpcClient opcClient) : base(opcClient)
        {
            //@"^.*_P[CT].*$"
            switch (opcClient)
            {
                case OpcClient _:
                    subStringTagName = @"^[S]\d{2,3}[_]\w*_P[CT]\d{2,3}$";
                    break;
                case OpcClientCitect _:
                    subStringTagName = @"^[S]\d{2,3}[_]\d{2,3}[_]\w*_P[CT]\d{2,3}[_]R$";
                    break;
                default:
                    break;
            }
            PressureList = new List<Pressure>();
        }

        protected internal override void CreateItemList()
        {
            //Считываем из OPC-Reader строки с названиями переменных
            nodeElementCollection = opcClient.ReadDataToNodeList(subStringTagName).ToList();
            
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

        // перегруженый метод создания списка давлений
        protected internal override void CreateItemList(HashSet<string> pressuesDB, List<OpcDaBrowseElement> opcDaBrowseElements)
        {
            //Считываем из OPC-Reader строки с названиями переменных
            foreach (var pressue in pressuesDB)
            {
                var temp = opcDaBrowseElements.FirstOrDefault(t => t.ItemId == pressue + "_R");
                if (temp != null)
                {
                    temp.Name = temp.Name.Replace("_R", "");
                    temp.ItemId = temp.Name.Replace("_R", "");
                    nodeElementCollection.Add(temp);
                }
            }
            countItems = nodeElementCollection.Count;
        }

        //Создаем группу для чтения из OPC-сервера
        protected internal override void CreateOPCReadGroup()
        {
            OpcDaItemResult[] readingResults;
            List<string> listOfValidItems;

            this.InitDataGroup(itemDescPressureForRead, "Pressures_Read_Group", nodeElementCollection, out dataGroupRead, out readingResults, out listOfValidItems);
            listOfValidItems.ForEach(i => PressureList.Add(new Pressure(i)));

        }

        //Обновляем список Pressure данными из OPC
        protected internal override void UpdateItemListFromOpc()
        {
            countOfErrorsInReading = 0;
            pressureValues = dataGroupRead.Read(dataGroupRead.Items, OpcDaDataSource.Device);

            int _valueCollectionIterator = 0;
            var _pressure = default(Pressure);
            foreach (var item in PressureList)
            {
                try
                {
                    //Initialization of fields of temperature instance
                    _pressure = PressureList.FirstOrDefault(c => c.TagName == item.TagName);
                    if (_pressure != null && pressureValues[0 + _valueCollectionIterator].Error.Succeeded)
                    {
                        _pressure.Val_R = (float)pressureValues[0 + _valueCollectionIterator].Value;
                    }
                    else
                    {
                        _pressure.IsInValid = true;
                        countOfErrorsInReading++;
                    }
                }
                catch (Exception)
                {
                    throw new Exception("Error in Pressures UpdateItemListFromOpc");
                }
                finally
                {
                    _valueCollectionIterator += itemDescPressureForRead.Length;
                }
                
            }
            //if (countOfErrorsInReading > 0)
            //    throw new Exception($"Количество ошибок чтения давлений - {countOfErrorsInReading}");
        }

        



    }
}
