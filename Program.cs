using System;
using System.Threading;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Net.Mail;
using System.Numerics;
using GeoConvert;
using One_Sgp4;
using System.Linq;
using System.Net.Http.Headers;
using System.Collections.Immutable;
using System.Net;
using System.IO;

namespace DistToSatellite
{
    class Program
    {
        static void Main(string[] args)
        {

            if (!File.Exists(Environment.CurrentDirectory + "/LastUpdateFile.txt"))
            {
                File.Create(Environment.CurrentDirectory + "/LastUpdateFile.txt");
            }

            if (!File.Exists(Environment.CurrentDirectory + "/TLEData.txt"))
            {
                File.Create(Environment.CurrentDirectory + "/TLEData.txt");

                UpdateTLEData();
            }

            //My coordinates: 51.4 degrees, 0.6 degrees, 60m ASL

            Console.WriteLine("Please enter your latitude");
            double latitude = Convert.ToDouble(Console.ReadLine());
            Console.WriteLine("Please enter your longitude");
            double longitude = Convert.ToDouble(Console.ReadLine());
            Console.WriteLine("Please enter your height above sea level");
            double height = Convert.ToDouble(Console.ReadLine());
            Console.WriteLine();

            while (1 == 1)
            {               
                FindClosestSatellite(latitude, longitude, height);
                System.Threading.Thread.Sleep(2000);
            }
        }

        public static void FindClosestSatellite(double userLat, double userLong, double userHeight)
        {
            UpdateTLEData();

            List<Tle> tleList = ParserTLE.ParseFile("TLEData.txt");

            Vector3 difference = new Vector3(0, 0, 0);

            Satellite closest = new Satellite("Temporary Satellite. If you see this, you done messed up", double.MaxValue);
            List<Satellite> satellites = new List<Satellite>();

            One_Sgp4.Coordinate observer = new Coordinate(userLat, userLong, userHeight);

            for (int i = 0; i < tleList.Count; i++)
            {
                //Create Time points
                EpochTime startTime = new EpochTime(DateTime.UtcNow);
                EpochTime stopTime = new EpochTime(DateTime.UtcNow.AddHours(1));

                One_Sgp4.Sgp4 sgp4Propagator = new Sgp4(tleList[i], Sgp4.wgsConstant.WGS_84);

                try
                {
                    sgp4Propagator.runSgp4Cal(startTime, stopTime, 0.5);
                }
                catch
                {
                    Console.WriteLine("Something went wrong with " + tleList[i].getName());
                }

                List<One_Sgp4.Sgp4Data> resultDataList = new List<Sgp4Data>();
                //Return Results containing satellite Position x,y,z (ECI-Coordinates in Km) and Velocity x_d, y_d, z_d (ECI-Coordinates km/s) 
                resultDataList = sgp4Propagator.getResults();

                try
                {
                    double localSiderealTime = startTime.getLocalSiderealTime(observer.getLongitude());

                    Sgp4Data grrPoint = One_Sgp4.SatFunctions.getSatPositionAtTime(tleList[0], startTime, Sgp4.wgsConstant.WGS_84);

                    One_Sgp4.Point3d sphCoordsSat = One_Sgp4.SatFunctions.calcSphericalCoordinate(observer, startTime, resultDataList[0]);

                    double distance = sphCoordsSat.x;

                    Satellite satellite = new Satellite(tleList[i].getName(), distance);
                    satellites.Add(satellite);

                    if (distance < closest.distance)
                    {
                        closest = satellite;
                    }
                }
                catch
                {
                    //Something went wrong with a satellite, skipped
                }
            }

            Console.WriteLine("Done: " + satellites.Count + " satellites successfully analyzed from " + tleList.Count + " in the dataset.");
            Console.WriteLine("The closest active satellite is " + closest.name.Trim() + " which is " + Math.Round(closest.distance, 2).ToString() + " kilometres away from you. Time: " + DateTime.Now.ToString("HH:mm:ss"));
            Console.WriteLine();
        }

        class Satellite
        {
            public string name = "";
            public double distance;

            public Satellite(string varName, double distNew)
            {
                name = varName;
                distance = distNew;
            }
        }

        public static void UpdateTLEData()
        {
            string url = "http://www.celestrak.com/NORAD/elements/active.txt";
            string pathToDate = Environment.CurrentDirectory + @"\LastUpdateFile.txt";
            string pathToTLE = Environment.CurrentDirectory + @"\TLEData.txt";
            string dateText = File.ReadAllText(pathToDate);
            string htmlText;

            //File.WriteAllText(pathToDate, DateTime.UtcNow.AddHours(-12).ToString());

            DateTime lastUpdate = DateTime.Parse(dateText);

            if ((DateTime.UtcNow - lastUpdate).TotalHours >= 6)
            {
                WebClient wb = new WebClient();
                htmlText = wb.DownloadString(url);
                lastUpdate = DateTime.UtcNow;
                File.WriteAllText(pathToDate, lastUpdate.ToString());
                File.WriteAllText(pathToTLE, htmlText);
                Console.WriteLine("Updated TLE data");
            }
            else
            {
                htmlText = "";
            }
        }
    }
}
