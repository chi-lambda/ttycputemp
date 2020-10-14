using System;
using System.IO;
using System.Linq;

namespace ttycputemp
{
    public class Reading
    {
        public int MaxTemperature { get; }
        public int MaxHeight { get; private set;}
        public int AvgTemperature { get; }
        public int AvgHeight { get; private set; }
        public int MinTemperature { get; }
        public int MinHeight { get; private set; }

        public int[] Temperatures { get; }
        public int[] Heights { get; private set; }
        public DateTime Time { get; set; }

        private const string thermalDir = "/sys/class/thermal/";
        public Reading()
        {
            MaxTemperature = Directory
                .EnumerateDirectories(thermalDir, "thermal_zone*")
                .Select(dir => int.Parse(File.ReadAllText(Path.Combine(thermalDir, dir, "temp"))) / 1000)
                .Max();

            AvgTemperature = (int)Math.Round(Directory
                .EnumerateDirectories(thermalDir, "thermal_zone*")
                .Select(dir => int.Parse(File.ReadAllText(Path.Combine(thermalDir, dir, "temp"))) / 1000)
                .Average());

            MinTemperature = Directory
                .EnumerateDirectories(thermalDir, "thermal_zone*")
                .Select(dir => int.Parse(File.ReadAllText(Path.Combine(thermalDir, dir, "temp"))) / 1000)
                .Min();

            Temperatures = Directory
                .EnumerateDirectories(thermalDir, "thermal_zone*")
                .Select(dir => int.Parse(File.ReadAllText(Path.Combine(thermalDir, dir, "temp"))) / 1000)
                .ToArray();

            Time = DateTime.Now;
        }

        public void ComputeHeights(int maxTemp, int height)
        {
            MaxHeight = height * (maxTemp - MaxTemperature) / maxTemp;
            AvgHeight = height * (maxTemp - AvgTemperature) / maxTemp;
            MinHeight = height * (maxTemp - MinTemperature) / maxTemp;
            Heights = Temperatures.Select(temp => height * (maxTemp - temp) / maxTemp).ToArray();
        }
    }
}