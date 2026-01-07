using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using TechParamsCalc.Factory;
using TechParamsCalc.Parameters;
using TechDotNetLib.Lab.Substances.ContentCalculation;
using System.Drawing.Text;

namespace TechParamsCalc.Controllers
{
    //Класс для дополнительных расчетов
    internal class AdditionalCalculator
    {
        TemperatureCreator temperatureCreator;
        PressureCreator pressureCreator;
        DensityCreatorCitect densityCreator;
        CapacityCreatorCitect capacityCreator;
        ContentCreatorCitect contentCreator;
        SingleTagCreator singleTagCreator;

        private bool isInitPropyleneSuccess;
        private bool isInitDeltaPSuccess;


        internal AdditionalCalculator(ItemsCreator temperatureCreator, ItemsCreator pressureCreator, ItemsCreator densityCreator, ItemsCreator capacityCreator, ItemsCreator contentCreator, ItemsCreator singleTagCreator)
        {
            this.temperatureCreator = temperatureCreator as TemperatureCreator;
            this.pressureCreator = pressureCreator as PressureCreator;
            this.densityCreator = densityCreator as DensityCreatorCitect;
            this.capacityCreator = capacityCreator as CapacityCreatorCitect;
            this.contentCreator = contentCreator as ContentCreatorCitect;
            this.singleTagCreator = singleTagCreator as SingleTagCreator;

            //Инициализируем расчет по расходу пропилена
            isInitPropyleneSuccess = InitalizePropyleneCalculations();
            isInitDeltaPSuccess = InitalizeDeltaPCalculations();
            //InitalizePOPCalculations_T03_T06();
            //InitalizePOPCalculations_S13_P03();
            //InitalizePOPCalculations_T06_D08();

        }

        #region Расчет массы пропилена в 1й реакционной смеси (22.05.2020)

        private Density acnWaterDensity;
        private Density tempDensity;
        private Temperature acnWaterTemperature;
        private Temperature acnWaterPropyleneTemperature;

        internal void CalculateMassOfPropylene()
        {

            double propyleneMass;

            if (!isInitPropyleneSuccess)
            {
                return;
            }

            //Проверка на наличие минимального расхода ACN и воды, а также, чтобы расход из P05 был выше, чем из D02
            if ((singleTagCreator.AcnWaterMassFlow < 408.85 * densityCreator.DensityList.FirstOrDefault(d => d.TagName == "S11_A01_FC02_DENS").ValHmi * 0.0001) ||
                singleTagCreator.averReactFlow < singleTagCreator.AcnWaterMassFlow / (densityCreator.DensityList.FirstOrDefault(d => d.TagName == "S11_A01_FC02_DENS").ValHmi * 0.0001) + 1)
            {
                propyleneMass = 0.0;
            }
            else
            {
                var waterMass = singleTagCreator.AcnWaterMassFlow * acnWaterDensity.PercArray[0] * 0.01;
                var acnMass = singleTagCreator.AcnWaterMassFlow * acnWaterDensity.PercArray[1] * 0.01;

                //Инициализурем расчитываемую плотность стартовыми значениями (без пропилена - только вода и ACN
                tempDensity.PercArray[0] = acnWaterDensity.PercArray[0];
                tempDensity.PercArray[1] = acnWaterDensity.PercArray[1];
                tempDensity.PercArray[2] = 0.0;

                var dens = tempDensity.CalculateDensity();

                var massAverReactFlowSave = singleTagCreator.averReactFlow * dens * 0.0001;
                var massAverReactFlow = 0.0;
                var diff = 0.0;
                //Подставляем для расчета плотности температуру после P05
                tempDensity.Temperature = acnWaterPropyleneTemperature;
                var i = 0;
                do
                {   //Рассчитываем массовый расход реакционной смеси и % компонентов в ней                

                    if (massAverReactFlowSave != 0)
                    {
                        tempDensity.PercArray[0] = Math.Max(0.0, Math.Min(100.0, waterMass * 100.0 / massAverReactFlowSave));
                        tempDensity.PercArray[1] = Math.Max(0.0, Math.Min(100.0, acnMass * 100.0 / massAverReactFlowSave));
                        tempDensity.PercArray[2] = Math.Max(0.0, 100.0 - tempDensity.PercArray[1] - tempDensity.PercArray[0]);
                    }


                    //Считаем плотность с новым содержанием компонентов
                    dens = tempDensity.CalculateDensity();
                    if (dens != -1)
                    {
                        massAverReactFlow = singleTagCreator.averReactFlow * dens * 0.0001;
                        propyleneMass = massAverReactFlow * tempDensity.PercArray[2] * 0.01;
                        diff = Math.Abs(massAverReactFlowSave - massAverReactFlow);
                        massAverReactFlowSave = massAverReactFlow;
                        i++;
                    }
                    else return;
                }
                while (diff > 1.0 || i < 10);

            }

            singleTagCreator.PropyleneMass = (short)(propyleneMass * 10.0);
        }

        private bool InitalizePropyleneCalculations()
        {
            var isInitSuccess = false;

            acnWaterDensity = densityCreator.DensityList.FirstOrDefault(d => d.TagName == "S11_A01_FC02_DENS");
            acnWaterTemperature = temperatureCreator.TemperatureList.FirstOrDefault(t => t.TagName == "S11_E28_TT01");
            acnWaterPropyleneTemperature = temperatureCreator.TemperatureList.FirstOrDefault(t => t.TagName == "S11_P05_TT01");

            if (acnWaterDensity == null || acnWaterTemperature == null || acnWaterPropyleneTemperature == null)
            {
                return false;
            }

            tempDensity = new Density(new string[] { "Water", "ACN", "P" }, new double[] { 5, 95, 0 }, acnWaterTemperature);
            isInitSuccess = true;

            return isInitSuccess;
        }
        #endregion

        #region Расчет дельты к заданному давлению в 1.E06 и дельты к заданному давлению реакции
        //Для дельты к заданному давлению в 1.E06
        Pressure S11_A01_PT01;
        Temperature S11_P05_TT01;

        //Для дельты к заданному давлению реакции
        Pressure S11_P05_PT01;

        internal double CalculateDeltaP(float _tt, float _pt)
        {
            _pt = Math.Max(15.0f, Math.Min(_pt, 20.0f));
            _tt = Math.Max(33.0f, Math.Min(_tt, 90.0f));

            List<double> pressureList = new List<double> { 15.0, 16.0, 17.0, 18.0, 19.0, 20.0 }; //7
            List<CoefSet> coefListPressure = new List<CoefSet>();  //Для давлений

            coefListPressure.Add(new CoefSet { a0 = 0.92022056, a1 = -0.085198918, a2 = 0.0020543869, a3 = -0.0000084876782, a4 = 0.0, a5 = 0.0 }); //0
            coefListPressure.Add(new CoefSet { a0 = 1.0996157, a1 = -0.091265026, a2 = 0.002068102, a3 = -0.0000084679355, a4 = 0.0, a5 = 0.0 }); //1
            coefListPressure.Add(new CoefSet { a0 = 1.2858471, a1 = -0.097478466, a2 = 0.0020827365, a3 = -0.000008449405, a4 = 0.0, a5 = 0.0 }); //2
            coefListPressure.Add(new CoefSet { a0 = 1.4729132, a1 = -0.10378968, a2 = 0.002097827, a3 = -0.0000084303312, a4 = 0.0, a5 = 0.0 }); //3
            coefListPressure.Add(new CoefSet { a0 = 1.6621591, a1 = -0.11032376, a2 = 0.0021155911, a3 = -0.0000084226318, a4 = 0.0, a5 = 0.0 }); //4
            coefListPressure.Add(new CoefSet { a0 = 1.8629333, a1 = -0.11702339, a2 = 0.0021340842, a3 = -0.0000084147094, a4 = 0.0, a5 = 0.0 }); //5

            //Определяем номер формулы (по давлению - линейная интерполяция)
            var numOfRange = ContentCalc.GetNumOfFormula(pressureList, _pt, out double deviation);
            double delta;

            //Вичисляем содержание

            //Если попали в точку базового давления-
            if (1 - deviation < 0.1 || deviation == 0)
                //Считаем по конкретной формуле один раз
                delta = ContentCalc.getPolynomValue(_tt, coefListPressure[numOfRange]);

            //Если переданное давление ниже минимального в массиве -
            else if (numOfRange == 0)
            {
                //Считаем по формуле №0               
                delta = ContentCalc.getPolynomValue(_tt, coefListPressure[0]);
            }

            //Если переданное давление - больше максимального в массиве - 
            else if (numOfRange == pressureList.Count)
            {
                //Считаем по формуле №pressureList.Count - 1
                delta = ContentCalc.getPolynomValue(_tt, coefListPressure[pressureList.Count - 1]);
            }


            else
            {
                //Считем по двум формулам
                double tmpcount_1 = ContentCalc.getPolynomValue(_tt, coefListPressure[numOfRange - 1]);
                double tmpcount_2 = ContentCalc.getPolynomValue(_tt, coefListPressure[numOfRange]);
                delta = tmpcount_1 + (tmpcount_2 - tmpcount_1) * deviation;
            }

            return Math.Max(0, delta);

        }
        private bool InitalizeDeltaPCalculations()
        {
            //Delta E06
            S11_A01_PT01 = pressureCreator.PressureList.FirstOrDefault(p => p.TagName == "S11_A01_PT01");
            S11_P05_TT01 = temperatureCreator.TemperatureList.FirstOrDefault(t => t.TagName == "S11_P05_TT01");

            //Delta R01
            S11_P05_PT01 = pressureCreator.PressureList.FirstOrDefault(p => p.TagName == "S11_P05_PT01");

            return true;
        }
        #endregion

        #region Расчет соотношения перекиси к реакционной смеси 1 для поддержания азеотропной концентрации в 1.Т01 + крепость ACN
        internal double[] CalculatePeroxideRatioAcnStrength()
        {

            //-----------------------Расчет крепости ACN по массовому расходу 100% перекиси---------------------------

            //Массовый расход воды из расствора ACN-Water в 1.D02, кг/час
            var flowMassWaterFromAcn = singleTagCreator.S11_A01_FC02_AVER_HMI * (1 - singleTagCreator.S11_D02_AP01_HMI * 0.01);

            double GetActualAcnStrength(double _peroxide100Mass)
            {
                //Массовый расход воды, освободившейся после реакции, кг/час
                var flowMassWaterFromReaction = _peroxide100Mass * 18.0153 / 34.0147; //Формулы стехиометрии реакции H2O2 + P = PO + H2O. См. файл "Теплово1 эффект реакции"

                //Общий массовый расход воды, кг/час
                var flowMassWaterTotal = _peroxide100Mass * 100.0 / singleTagCreator.S12_P02_AP01_HMI - _peroxide100Mass + flowMassWaterFromAcn + flowMassWaterFromReaction;
                var str = (1 - flowMassWaterTotal / (flowMassWaterTotal + singleTagCreator.S11_A01_FC02_AVER_HMI - flowMassWaterFromAcn)) * 100.0;

                return str;
            }


            //--------------------Расчет % соотношения перекись/реакционная смесь 1 ------------------------------

            //Стартовый заданный % перекиси к реакционной смеси 1, %
            var startPercentOfPeroxydeInMix = 0.0;

            //Расчетная крепость ACN при стартовом заданном  % перекиси к реакционной смеси 1, %
            var acnStrength = 0.0;

            //Расчетный массовый расход перекиси (Fреакционной смеси * Заданный процент), кг/час
            var peroxide100Mass = 0.0;

            var strengthAzeo = singleTagCreator.S11_T01_PT05_AZEO_HMI;

            var i = 0;
            //Рекурсия :)            
            double GetRatio(double step)
            {
                if (step < 0.001)
                    return startPercentOfPeroxydeInMix + step * 10.0 - step;

                else
                {
                    while (true)
                    {
                        //Расчитываем крепость ACN для массового расхода 100% перекиси, рассчитанного по коєффициенту перекись/реакционная смесь 1 
                        peroxide100Mass = singleTagCreator.S11_P05_FC01_HMI * startPercentOfPeroxydeInMix * 0.01;
                        acnStrength = GetActualAcnStrength(peroxide100Mass);

                        if (acnStrength < strengthAzeo + 0.1 || i > 200)
                        {
                            startPercentOfPeroxydeInMix = startPercentOfPeroxydeInMix + step / 10.0 - step;
                            break;
                        }

                        i++;
                        startPercentOfPeroxydeInMix = startPercentOfPeroxydeInMix + step;
                    }
                    return GetRatio(step / 10.0);
                }
            }
            //--------------------------------------------------------------------------------------------------------
            var ratio = GetRatio(1.0);
            var strength = GetActualAcnStrength(singleTagCreator.S12_P02_FT01_SP * singleTagCreator.S12_P02_AP01_HMI * 0.01);

            return new double[] { ratio, strength };
        }
        #endregion


        #region Старый код. Удалить при необходимости
        //internal double CalculateOPForD08()
        //{
        //    var newDens = 0.0;
        //    var POContent = 0.0;

        //    if (PoD08_DENS == null)
        //        return -1.0;

        //    PoD08_DENS.PercArray[0] = 13.0;   //Water
        //    PoD08_DENS.PercArray[1] = 87.0;   //ACN
        //    PoD08_DENS.PercArray[2] = 0.0;    //P
        //    PoD08_DENS.PercArray[3] = 0.0;    //PO

        //    PoD08_DENS.Temperature.Val_R = singleTagCreator.S11_P13_FT01_Mass_TEMPERATURE;

        //    int i = 0;
        //    while (true)
        //    {
        //        newDens = PoD08_DENS.CalculateDensity();
        //        if (newDens > singleTagCreator.S11_P13_FT01_Mass_DENSITY * 10.0 || i++ > 2000)
        //        {
        //            POContent = PoD08_DENS.PercArray[3];
        //            break;
        //        }
        //        PoD08_DENS.PercArray[3] += 0.05;
        //        PoD08_DENS.PercArray[1] = (100.0 - PoD08_DENS.PercArray[3] - PoD08_DENS.PercArray[2]) * 0.87;
        //        PoD08_DENS.PercArray[0] = 100.0 - PoD08_DENS.PercArray[1] - PoD08_DENS.PercArray[2] - PoD08_DENS.PercArray[3];
        //    }
        //    return Math.Min(100.0, POContent);
        //}


        //internal double CalculateOPFor_S13_P03()
        //{
        //    var newDens = 0.0;
        //    var POContent = 0.0;

        //    if (S13_P03_FC01_DENS == null)
        //        return -1.0;            

        //    S13_P03_FC01_DENS.PercArray[0] = 13.0;   //Water
        //    S13_P03_FC01_DENS.PercArray[1] = 87.0;   //ACN
        //    S13_P03_FC01_DENS.PercArray[2] = 0.0;    //P
        //    S13_P03_FC01_DENS.PercArray[3] = 0.0;    //PO                       

        //    int i = 0;
        //    while (true)
        //    {
        //        newDens = S13_P03_FC01_DENS.CalculateDensity();
        //        if (newDens > singleTagCreator.S13_P03_FT01_Mass_DENSITY * 10.0 || i++ > 2000)
        //        {
        //            POContent = S13_P03_FC01_DENS.PercArray[3];
        //            break;
        //        }
        //        S13_P03_FC01_DENS.PercArray[3] += 0.05;
        //        S13_P03_FC01_DENS.PercArray[1] = (100.0 - S13_P03_FC01_DENS.PercArray[3] - S13_P03_FC01_DENS.PercArray[2]) * 0.87;
        //        S13_P03_FC01_DENS.PercArray[0] = 100.0 - S13_P03_FC01_DENS.PercArray[1] - S13_P03_FC01_DENS.PercArray[2] - S13_P03_FC01_DENS.PercArray[3];
        //    }
        //    return Math.Min(100.0, POContent);
        //}        
        #endregion


        #region Итеративный расчет содержания PO (общая функция)

        //Расчет крепости PO от колонны 1.Т03 к колонне 1.Т06
        private Density S11_T06_FT02_DENS_ADD; 
        private Density S11_T06_FT02_DENS_ADD2;

        private bool InitalizePOPCalculations_T03_T06()
        {            
            S11_T06_FT02_DENS_ADD = densityCreator.DensityList.FirstOrDefault(d => d.TagName == "S11_T03_AP03_DENS");
            if (S11_T06_FT02_DENS_ADD != null)
            {
                S11_T06_FT02_DENS_ADD.Temperature = new Temperature("tmpTemp", singleTagCreator.S11_T06_FT02_Mass_TEMPERATURE);
                S11_T06_FT02_DENS_ADD2 = new Density(new string[] { "Water", "P", "P", "PO" }, new double[4], S11_T06_FT02_DENS_ADD.Temperature); //Было "Water", "ACN", "P", "PO"
            }

            return S11_T06_FT02_DENS_ADD != null && S11_T06_FT02_DENS_ADD2 != null;
        }

        //Расчет крепости пропиленоксида из склада в колонну 1.Т01 (итерационый расчет)
        private Density S13_P03_FC01_DENS_ADD;
        private Density S13_P03_FC01_DENS_ADD2;

        private bool InitalizePOPCalculations_S13_P03()
        {
            var temperature = temperatureCreator.TemperatureList.FirstOrDefault(t => t.TagName == "S13_P03_TC02");

            if (temperature != null)
            {
                S13_P03_FC01_DENS_ADD = new Density(new string[] { "Water", "P", "ACN", "PO" }, new double[4], temperature);
                S13_P03_FC01_DENS_ADD2 = new Density(new string[] { "Water", "P", "P", "PO" }, new double[4], temperature);
            }

            return S13_P03_FC01_DENS_ADD != null && S13_P03_FC01_DENS_ADD2 != null;
        }

        //Расчет крепости PO к сборнику 1.D08 от колонны 1.Т06;
        private Density S11_P13_FT01_DENS_ADD;
        private Density S11_P13_FT01_DENS_ADD2;

        private bool InitalizePOPCalculations_T06_D08()
        {
            S11_P13_FT01_DENS_ADD = densityCreator.DensityList.FirstOrDefault(d => d.TagName == "S11_D08_AP01_DENS");
            if (S11_P13_FT01_DENS_ADD != null)
            {
                S11_P13_FT01_DENS_ADD.Temperature = new Temperature("tmpTemp", singleTagCreator.S11_P13_FT01_Mass_TEMPERATURE);
                S11_P13_FT01_DENS_ADD2 = new Density(new string[] { "Water", "P", "P", "PO" }, new double[4], S11_P13_FT01_DENS_ADD.Temperature);
            }

            return S11_P13_FT01_DENS_ADD != null && S11_P13_FT01_DENS_ADD2 != null;
        }


        //Итеративный расчет содержания PO (общая функция)
        private (double, double[]) CalculateStrength(double waterContent, double acnContent, float densityFromMassFT, ref Density inputDensity, bool isIncrement)
        {
            //isDecrement = true - итерация в сторону увеличения; false - итерация в сторону уменьшения
            var newDens = isIncrement ? 0.0 : 10000.0;
            var POContent = 0.0;
            var percArray = new double[4] { waterContent, acnContent, 0.0, 0.0 }; //ACN, Water, P, PO
            inputDensity.PercArray = percArray;

            if (inputDensity == null)
                return (-1.0, new double[] { -1.0, -1.0, -1.0, -1.0 });

            int i = 0;

            //Формирование условия выхода из цикла в зависимости от направления итерации
            bool GetExitCondition()
            {
                return isIncrement ? newDens > densityFromMassFT * 10.0 : newDens < densityFromMassFT * 10.0;
            }

            while (true)
            {
                //newDens = inputDensity.CalculateDensity();
                if (GetExitCondition() || i++ > 10000)
                {
                    POContent = inputDensity.PercArray[3];
                    break;
                }
                inputDensity.PercArray[3] += 0.01;                                                                                        //PO
                inputDensity.PercArray[1] = (100.0 - inputDensity.PercArray[3] - inputDensity.PercArray[2]) * acnContent * 0.01;          //ACN
                inputDensity.PercArray[0] = 100.0 - inputDensity.PercArray[1] - inputDensity.PercArray[2] - inputDensity.PercArray[3];   //Water
                newDens = inputDensity.CalculateDensity();
            }
            return (Math.Min(100.0, POContent), inputDensity.PercArray);
        }
        #endregion

        //Все дополнительные расчеты
        internal void CalculateParameters()
        {

            CalculateMassOfPropylene();

            //1. Расчет дельты к заданному давлению в 1.E06 по ТТ и PT после теплообменника 1.E32           

            singleTagCreator.DeltaPE06 = (short)(CalculateDeltaP(S11_P05_TT01?.Val_R ?? -1, S11_A01_PT01?.Val_R ?? -1) * 100.0);

            //2. Расчет дельты к заданному давлению реакции по давлению в 1.А01 (S11_P05_PT01) и заданию темп-ры в реакторе 1.R01(S11_R01_TT01_SP)
            singleTagCreator.DeltaPR01 = (short)(CalculateDeltaP(singleTagCreator.S11_R01_TT01_SP, S11_P05_PT01?.Val_R ?? -1) * 100);

            //3. Расчет соотношения перекиси к реакционной смеси 1 для поддержания азеотропной концентрации в 1.Т01
            var ratioStrengthVar = CalculatePeroxideRatioAcnStrength();

            singleTagCreator.PeroxideMixRatio = (short)(Math.Min(ratioStrengthVar[0], 32.0) * 1000);
            singleTagCreator.AcnStrength = (short)(Math.Min(ratioStrengthVar[1], 100.0) * 100);

            
            //4. Расчет крепости PO от колонны 1.Т03 к колонне 1.Т06
            S11_T06_FT02_DENS_ADD.Temperature.Val_R = S11_T06_FT02_DENS_ADD2.Temperature.Val_R = singleTagCreator.S11_T06_FT02_Mass_TEMPERATURE;    //При каждой итерации расчета читаем заново тег температуры
            

            var t03_t06_strngth_87 = CalculateStrength(25.0, 75.0, singleTagCreator.S11_T06_FT02_Mass_DENSITY - 7.5f, ref S11_T06_FT02_DENS_ADD2,  true);  //Содержание PO при 87% ACN в смеси ACN-Water
            var t03_t06_strngth_0  = CalculateStrength(50.0, 50.0, singleTagCreator.S11_T06_FT02_Mass_DENSITY, ref S11_T06_FT02_DENS_ADD, true);    //Содержание PO при 50% ACN и 50% альдегидов в смеси Water-альдегиды (вместо альдегидов подставленный P)

            singleTagCreator.PoStrengthT03_T06_87PercAcn = (short)(t03_t06_strngth_87.Item1 * 100.0);
            singleTagCreator.PoStrengthT03_T06_0PercAcn  = (short)(t03_t06_strngth_0.Item1 * 100.0);   
            
            singleTagCreator.S11_T06_FT02_PERC = t03_t06_strngth_0.Item2.Select(a => (short)Math.Max(0, Math.Min(10000.0, a * 100.0))).ToArray(); 

            //5. Расчет крепости PO со склада на колонну 1.T01  
                                                                                                                                                    //Не подставляем тег температуры т.к. температура уже входит в состав DENSITY S13_P03_FC01_DENS_ADD S13_P03_FC01_DENS_ADD как AI
            var p13_strngth_87 = CalculateStrength(13.0, 87.0, singleTagCreator.S13_P03_FT01_Mass_DENSITY, ref S13_P03_FC01_DENS_ADD, true);        //Содержание PO при 87% ACN в смеси ACN-Water
            var p13_strngth_0  = CalculateStrength(100, 0.0, singleTagCreator.S13_P03_FT01_Mass_DENSITY, ref S13_P03_FC01_DENS_ADD, false);         //Содержание PO при 0% ACN в смеси ACN-Water

            singleTagCreator.PoStrengthP03_87PercAcn = (short)(p13_strngth_87.Item1 * 100.0);
            singleTagCreator.PoStrengthP03_0PercAcn  = (short)(p13_strngth_0.Item1 * 100.0);
            singleTagCreator.S13_P03_FT01_PERC       = p13_strngth_0.Item2.Select(a => (short)Math.Max(0, Math.Min(10000.0, a * 100.0))).ToArray();

            //6. Расчет крепости PO к сборнику 1.D08 от колонны 1.Т06;
            S11_P13_FT01_DENS_ADD.Temperature.Val_R = S11_P13_FT01_DENS_ADD2.Temperature.Val_R = singleTagCreator.S11_P13_FT01_Mass_TEMPERATURE;    //При каждой итерации расчета читаем заново тег температуры

            //Было 13, 87 28.02.2021
            var t06_d08_strngth_87 = CalculateStrength(singleTagCreator.S11_T06_AP01_START_WATER * 0.01, singleTagCreator.S11_T06_AP01_START_ALD * 0.01, singleTagCreator.S11_P13_FT01_Mass_DENSITY + 0.0f, ref S11_P13_FT01_DENS_ADD2, true);    //Содержание PO при 0% ACN в смеси ACN-Water
            var t06_d08_strngth_0  = CalculateStrength(100.0, 0.0, singleTagCreator.S11_P13_FT01_Mass_DENSITY, ref S11_P13_FT01_DENS_ADD, false);  //Содержание PO при 0% ACN в смеси ACN-Water

            singleTagCreator.PoStrengthT06_D08_87PercAcn = (short)(t06_d08_strngth_87.Item1 * 100.0);
            singleTagCreator.PoStrengthT06_D08_0PercAcn = (short)(t06_d08_strngth_0.Item1 * 100.0);

            singleTagCreator.S11_P13_2_FT01_PERC = t06_d08_strngth_0.Item2.Select(a => (short)Math.Max(0, Math.Min(10000.0, a * 100.0))).ToArray();
            
        }
    }
}
