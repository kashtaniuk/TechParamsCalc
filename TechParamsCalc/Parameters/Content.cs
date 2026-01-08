using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using TechDotNetLib;
using TechDotNetLib.Lab.Substances;

namespace TechParamsCalc.Parameters
{
    internal class Content : Characteristic
    {
        public short Sel { get; set; }
        public short ValHmi { get; set; }
        public short[] ValCalc { get; set; }
        public short[] DeltaT { get; set; } = new short[5];
        public short[] DeltaP { get; set; } = new short[5];
        public string[] PercDescription { get; set; } = new string[5];

        //configurationCode = 10 : разряд единиц - признак снятия ограничения 0-100% (0 - не снято; 1 - снято); Разряд десятков - выбор формулы для расчетов
        public short Conf { get; set; }
        public Temperature Temperature { get; set; }
        public Pressure Pressure { get; set; }
        public short AtmoPressure { get; set; }

        private Mix mix;
        public static int id = 0;

        public Content(string tagName) : base(tagName)
        {
            Id = ++id;
        }

        //Метод расчета крепости с подключением библиотеки TechDotNetLib
        public double[] CalculateContent()
        {
            double[] _contentValue = new double[5];

            try
            {
                mix = new Mix(PercDescription);

                try
                {
                    _contentValue = mix.GetContent((float)(Temperature.Val_R + DeltaT[0] * 0.1), (float)(Pressure?.Val_R + DeltaP[0] * 0.01 + AtmoPressure * 0.0001f), Conf);
                }
                catch (Exception)
                {
                    _contentValue = new double[] { -1.0, -1.0, -1.0, -1.0, -1.0 };
                }

            }
            catch (Exception)
            {
                MessageBox.Show("Content calculation error!", "Alert!", MessageBoxButton.OK, MessageBoxImage.Error);
            }

            ValCalc = Array.ConvertAll(_contentValue, el => (short)el);
            return _contentValue;

        }
    }
}
