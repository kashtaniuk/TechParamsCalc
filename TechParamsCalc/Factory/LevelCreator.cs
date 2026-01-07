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
    class LevelCreator : ItemsCreator
    {

        private string[] itemDescLevelForRead = new string[] { "R" };
        private string[] itemDescLevelForWrite = new string[0];

        private OpcDaItemValue[] levelValues;
        private int countOfErrorsInReading;

        public List<Level> LevelList { get; private set; }
        public LevelCreator(IOpcClient opcClient) : base(opcClient)
        {
            //@"^.*_L[CT].*$"
            //@"^.*_L[CT]\d{2,3}$"
            switch (opcClient)
            {
                case OpcClient _:
                    subStringTagName = @"^[S]\d{2,3}[_]\w*_L[CT]\d{2,3}$";
                    break;
                case OpcClientCitect _:
                    subStringTagName = @"^[S]\d{2,3}[_]\d{2,3}[_]\w*_L[CT]\d{2,3}[_]R$";
                    break;
                default:
                    break;
            }
            LevelList = new List<Level>();
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

        // перегруженый метод создания списка уровней
        protected internal override void CreateItemList(HashSet<string> levelDB, List<OpcDaBrowseElement> opcDaBrowseElements)
        {
            //Считываем из OPC-Reader строки с названиями переменных
            foreach (var level in levelDB)
            {
                var temp = opcDaBrowseElements.FirstOrDefault(t => t.ItemId == level + "_R" || t.ItemId == level);
                if (temp != null)
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

            this.InitDataGroup(itemDescLevelForRead, "Levels_Read_Group", nodeElementCollection, out dataGroupRead, out readingResults, out listOfValidItems);
            listOfValidItems.ForEach(i => LevelList.Add(new Level(i)));

        }

        //Обновляем список Levels данными из OPC
        protected internal override void UpdateItemListFromOpc()
        {
            countOfErrorsInReading = 0;
            //System.Windows.Forms.MessageBox.Show("Error with OPC Server reading data. Check Schneider OFS settings (IP address)", "Alert", MessageBoxButtons.OK, MessageBoxIcon.Error);
            levelValues = dataGroupRead.Read(dataGroupRead.Items, OpcDaDataSource.Device);

            int _valueCollectionIterator = 0;
            var _level = default(Level);

            foreach (var item in LevelList)
            {
                try
                {
                    //Initialization of fields of temperature instance
                    _level = LevelList.FirstOrDefault(t => t.TagName == item.TagName);
                    if (_level != null && levelValues[0 + _valueCollectionIterator].Error.Succeeded)
                    {
                        _level.Val_R = (float)levelValues[0 + _valueCollectionIterator].Value;
                    }
                    else
                    {
                        _level.IsInValid = true;
                        countOfErrorsInReading++;
                    }
                }
                catch (Exception)
                {
                    throw new Exception("Error in Levels UpdateItemListFromOpc");
                }
                finally
                {
                    _valueCollectionIterator += itemDescLevelForRead.Length;
                }

            }
            //if (countOfErrorsInReading > 0)
            //    throw new Exception($"Количество ошибок чтения уровней - {countOfErrorsInReading}");
        }
    }
}
