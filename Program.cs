using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mail;
using System.Threading;

namespace Cowin
{
    class Program
    {
        static void Main(string[] args)
        {

            var config = new ConfigurationBuilder()
                .SetBasePath(System.Environment.CurrentDirectory).AddJsonFile("appSettings.json", false, true).Build();

            var userConfig = config.GetSection("userConfig").Get<Userconfig>();
            var slotSettings = config.GetSection("slot").Get<Slot>();

            UInt64 totalCount = 0;
            while (true)
            {
                try
                {
                    using (var client = new HttpClient())
                    {
                        for (int i = 0; i < 10; i++)
                        {

                            var ageGroup = slotSettings.ageGroup;
                            var doseNumber = slotSettings.dose;
                            var today = DateTime.Now.AddDays(i).ToString("dd-MM-yyyy");

                            HttpResponseMessage response = client.GetAsync($"https://cdn-api.co-vin.in/api/v2/appointment/sessions/calendarByDistrict?district_id=363&date={today}&test={DateTime.Now.Ticks.ToString()}").Result;  // Blocking call!  
                            if (response.IsSuccessStatusCode)
                            {
                                var customerJsonString = response.Content.ReadAsStringAsync().Result;

                                var apiResponse = JsonConvert.DeserializeObject<ResponseObject>(custome‌​rJsonString);

                                if (apiResponse != null)
                                {
                                    foreach (var center in apiResponse.centers)
                                    {
                                        var slot = center.sessions.Where(x => ((x.available_capacity_dose2 > 0 && doseNumber == 2) || (x.available_capacity_dose1 > 0 && doseNumber == 1)) && x.min_age_limit == ageGroup).ToList();
                                        if (slot != null && slot.Count > 0)
                                        {
                                            string subject = $"{today} : {center.pincode} : {center.name}";
                                            string msg = "";
                                            msg += "<HTML>";
                                            msg += "<BODY>";

                                            msg += "<TABLE>";
                                            msg += $"<TR><TD>Date:</td><td>{today}</td></TR>";
                                            msg += $"<TR><TD>Age Group:</td><td>{ageGroup}</td></TR>";
                                            msg += $"<TR><TD>Dose:</td><td>{doseNumber}</td></TR>";
                                            msg += $"<TR><TD>Center:</td><td>{center.name}</td></TR>";
                                            msg += $"<TR><TD>Address:</td><td>{center.address}</td></TR>";
                                            msg += $"<TR><TD>Block:</td><td>{center.block_name}</td></TR>";
                                            msg += $"<TR><TD>PinCode:</td><td>{center.pincode}</td></TR>";
                                            msg += $"<TR><TD>FeeType:</td><td>{center.fee_type}</td></TR>";
                                            msg += "</TABLE>";

                                            msg += "</BODY>";
                                            msg += "</HTML>";

                                            Console.WriteLine(subject);

                                            //Send Email
                                            Email(subject, msg, userConfig.username, userConfig.pwd);
                                        }

                                    }
                                }

                            }

                            Thread.Sleep(1000);
                        }

                    }

                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{ex.Message} \n {ex.StackTrace.ToString()}");
                }
                Thread.Sleep(1000);
                Console.WriteLine($"{totalCount++}");
                Console.WriteLine($"=====================================================================================");
            }

            Console.WriteLine("Press any key to exit");
            Console.ReadKey(true);
        }

        public static void Email(string subject, string htmlString, string email, string pwd)
        {
            try
            {
                Console.WriteLine($"{email}:{pwd}");
                MailMessage message = new MailMessage();
                SmtpClient smtp = new SmtpClient();
                message.From = new MailAddress(email);
                message.To.Add(new MailAddress(" "));
                message.To.Add(new MailAddress(" "));
                message.Subject = subject;
                message.IsBodyHtml = true; //to make message body as html  
                message.Body = htmlString;
                smtp.Port = 587;
                smtp.Host = "smtp.gmail.com"; //for gmail host  
                smtp.UseDefaultCredentials = false;
                smtp.Credentials = new NetworkCredential(email, pwd);
                smtp.EnableSsl = true;
                smtp.DeliveryMethod = SmtpDeliveryMethod.Network;
                smtp.Send(message);
            }
            catch (Exception ex)
            {

                Console.WriteLine($"{ex.Message} \n {ex.StackTrace.ToString()}");
            }
        }
    }
}
