using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechParamsCalc.DataBaseConnection;
using TechParamsCalc.DataBaseConnection.Level;
using TechParamsCalc.OPC;
using TechParamsCalc.Parameters;
using TitaniumAS.Opc.Client.Da;
using TitaniumAS.Opc.Client.Da.Browsing;

namespace TechParamsCalc.Factory
{
    internal class LevelTankCreatorCitect : ItemsCreator
    {
        private string[] itemDescLevelTankForWrite = new string[] { "H_MAX", "H_HMI", "V_HMI", "M_HMI" };
        private string[] itemDescLevelTankForRead = new string[0];

        public List<LevelTank> LevelTankList { get; private set; }
        private SingleTagCreator singleTagCreator;
        //private OpcDaItemValue[] levelTankValues;

        public LevelTankCreatorCitect(IOpcClient opcClient, ItemsCreator itemCreator) : base(opcClient)
        {
            subStringTagName = @"^[S]\d{2,3}[_]\d{2,3}[_]\w*[_]TANK_H_HMI$";

            LevelTankList = new List<LevelTank>();

            singleTagCreator = itemCreator as SingleTagCreator;
        }


        //Метод для создания пустого списка переменных
        protected internal override void CreateItemList()
        {
            //Считываем из OPC-Reader строки с названиями переменных
            nodeElementCollection = opcClient.ReadDataToNodeList(subStringTagName).ToList();

            // удаляем _H_HMI
            foreach (var item in nodeElementCollection)
            {
                item.Name = item.Name.Replace("_H_HMI", "");
                item.ItemId = item.Name.Replace("_H_HMI", "");
            }
        }

        // перегруженый метод создания списка levelTank
        protected internal override void CreateItemList(HashSet<string> levelTankDB, List<OpcDaBrowseElement> opcDaBrowseElements)
        {
            //Считываем из OPC-Reader строки с названиями переменных
            foreach (var levelTank in levelTankDB)
            {
                var temp = opcDaBrowseElements.FirstOrDefault(t => t.ItemId == levelTank + "_TANK_H_HMI");
                if (temp != null)
                {
                    temp.Name = temp.Name.Replace("_H_HMI", "");
                    temp.ItemId = temp.Name.Replace("_H_HMI", "");
                    nodeElementCollection.Add(temp);
                }
            }
            countItems = nodeElementCollection.Count;
        }


        protected internal override void UpdateItemListFromOpc()
        {
            throw new NotImplementedException();
        }


        //Обновление LevelTank тегов из Базы Данных
        protected internal void UpdateItemListFromDB(List<Level> levelList, List<Density> densityList, DBPGContext dbContext)
        {

            //Создаем список анонимных объектов 
            var collection = (from tc in dbContext.tankContents
                              join t in dbContext.tanks on tc.tankId equals t.id
                              select new { Id = tc.id, TagName = tc.tankVarDef, Tank = t, tc.distanceA, tc.distanceB, tc.probeLength, tc.distToDistanceA }).ToList();

            //Создаем список LevelTank, параллельно инициализируем его переменными Level
            LevelTankList = new List<LevelTank>();
            collection.ForEach(x =>
            {
                var level = levelList.FirstOrDefault(l => l.TagName == x.TagName.Substring(0, Math.Max(x.TagName.IndexOf("_TANK"), 0)));
                var density = level != null ? densityList.FirstOrDefault(d => d.TagName == level.TagName + "_DENS") : null;

                if (level != null && density != null && nodeElementCollection.Any(ne => ne.Name == x.TagName))
                {
                    LevelTankList.Add(new LevelTank
                    {
                        Id = x.Id,
                        TagName = x.TagName,
                        Tank = x.Tank,
                        Level = level,
                        Density = density,
                        DistanceA = x.distanceA,
                        DistanceB = x.distanceB,
                        ProbeLength = x.probeLength,
                        DistToDistanceA = x.distToDistanceA,
                        IsWriteble = true
                    });
                }

            });

            //Инициализация объектов Tank значениями из LevelTank (distA, distB, LtoDistA)
            LevelTankList.ForEach((lt) =>
            {
                lt.Tank.InitalizeTank(lt.DistToDistanceA, lt.DistanceA, lt.DistanceB);
            });
        }


        //Создаем группу для записи в OPC-сервер  
        protected internal override void CreateOPCWriteGroup()
        {
            var LevelTanks = LevelTankList.Where(l => l.IsWriteble == true);
            //Принимаем, что список LevelTank сформировался на этапе формирования группы чтения и не содержит неправильных тегов
            this.InitDataGroup(itemDescLevelTankForWrite, "LevelTank_Write_Group", LevelTanks, out dataGroupWrite);


            //Инициализация массива объектов для будущей записи в OPC. Отбираются теги, значение "IsWriteble" == false
            this.valuesForWriting = new object[LevelTanks.Count() * itemDescLevelTankForWrite.Length]; //Массив значений для записи в OPC_сервер
        }



        //Запись тегов в OPC
        protected internal override void WriteItemToOPC()
        {
            int i = 0;
            foreach (var item in LevelTankList)
            {
                if (item.IsWriteble)
                {
                    valuesForWriting[i++] = (short)(item.DistanceB - item.DistanceA);
                    valuesForWriting[i++] = (short)item.LevelMm;
                    //item.DistanceB >= 2000 - если радиус сборника > 2м - записываем оьъем * 10
                    valuesForWriting[i++] = (short)(item.Tank.dimB >= 2000 ? item.Volume * 100 : item.Volume * 1000);
                    valuesForWriting[i++] = (short)(item.Tank.dimB >= 2000 ? item.Mass * 100 : item.Mass * 1000);
                }
            }

            if (opcClient.OpcServer.IsConnected)
                opcClient.WriteMultiplyItems(dataGroupWrite, valuesForWriting);
        }
    }
}
