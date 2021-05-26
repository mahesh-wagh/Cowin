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
using Amazon;
using Amazon.SimpleNotificationService;
using Amazon.SimpleNotificationService.Model;

namespace Cowin
{
    class Program
    {
        static void Main(string[] args)
        {

            var config = new ConfigurationBuilder()
                .SetBasePath(System.Environment.CurrentDirectory).AddJsonFile("appSettings.json", false, true).Build();

            var userConfig = config.GetSection("userConfig").Get<Userconfig>();
            var slotSettings = config.GetSection("slot").Get<IEnumerable<Slot>>();

            UInt64 totalCount = 0;
            while (true)
            {
                try
                {
                    using (var client = new HttpClient())
                    {
                        for (int i = 0; i < 15; i=i+7)
                        {
                            foreach (var slot in slotSettings)
                            {

                                var ageGroup = slot.ageGroup;
                                var doseNumber = slot.dose;
                                var today = DateTime.Now.AddDays(i).ToString("dd-MM-yyyy");

                                //session call
                                HttpResponseMessage response = client.GetAsync($"https://cdn-api.co-vin.in/api/v2/appointment/sessions/calendarByDistrict?district_id={slot.DistrictCode}&date={today}&test={DateTime.Now.Ticks.ToString()}").Result;

                                //public call
                                //HttpResponseMessage response = client.GetAsync($"https://cdn-api.co-vin.in/api/v2/appointment/sessions/public/calendarByDistrict?district_id={slot.DistrictCode}&date={today}&test={DateTime.Now.Ticks.ToString()}").Result;
                                if (response.IsSuccessStatusCode)
                                {
                                    var customerJsonString = response.Content.ReadAsStringAsync().Result;

                                    var apiResponse = JsonConvert.DeserializeObject<ResponseObject>(custome‌​rJsonString);

                                    if (apiResponse != null)
                                    {
                                        foreach (var center in apiResponse.centers)
                                        {
                                            var slotDetails = center.sessions.Where(x => ((x.available_capacity_dose2 > 1 && doseNumber == 2) || (x.available_capacity_dose1 > 1 && doseNumber == 1)) && x.min_age_limit == ageGroup).ToList();
                                            if (slotDetails != null && slotDetails.Count > 0)
                                            {

                                                string subject="", msg;

                                                //CreateEmailBody(ageGroup, doseNumber, today, center, slotDetails, out subject, out msg);

                                                CreateMsgBody(ageGroup, doseNumber, today, center, slotDetails, slot.mobiles);


                                                Console.WriteLine($"{subject}");

                                                //Send Email
                                                //Email(subject, msg, userConfig.username, userConfig.pwd, slot.to);

                                                Console.WriteLine($"Search Done\t: {today}\t||\tSlots found\t: { slotDetails.Count}");
                                            }

                                        }

                                        Console.WriteLine($"Search Done\t: {today}\t||\tCentres Scanned\t: { apiResponse.centers.Length}\t||\tLocation : { slot.DistrictName}\t({slot.ageGroup}:Dose-{slot.dose})");
                                        
                                    }
                                    else
                                    {
                                        Console.WriteLine($"Invalid Response\t: No Content");
                                        
                                    }
                                    Thread.Sleep(2000);
                                }
                                else if (response.StatusCode == HttpStatusCode.Unauthorized)
                                {
                                    Console.WriteLine($"Failed for \t: {today}\t||\tReason \t: {response.StatusCode}");
                                    Thread.Sleep(5000);
                                }
                                else if (response.StatusCode == HttpStatusCode.Forbidden)
                                {
                                    Console.WriteLine($"Failed for \t: {today}\t||\tReason \t: {response.StatusCode}");
                                    Thread.Sleep(31000);
                                }
                                else
                                {
                                    Console.WriteLine($"Failed for \t: {today}\t||\tReason \t: {response.StatusCode}");
                                    Thread.Sleep(15000);
                                }

                            }
                        }
                    }

                }
                catch (Exception ex)
                {
                    Console.WriteLine($"{ex.Message} \n {ex.StackTrace.ToString()}");
                }
                Console.WriteLine($"================================================================================================================");
                Console.WriteLine($"Round Completed\t: {++totalCount}");
                Console.WriteLine($"================================================================================================================");
                Thread.Sleep(5000);
            }

            Console.WriteLine("Press any key to exit");
            Console.ReadKey(true);
        }

        private static void CreateEmailBody(int ageGroup, int doseNumber, string today, Center center,List<Session> sessions, out string subject, out string msg)
        {
            subject = $"{today} : {center.pincode} : {center.name}";
            msg = "";
            msg += "<HTML>";
            msg += "<BODY>";
            msg += "<TABLE>";

            foreach (var session in sessions)
            {
                msg += $"<TR><TD></td><td></td></TR>";
                msg += $"<TR><TD></td><td></td></TR>";
                msg += $"<TR><TD>Date:</td><td><B>{session.date}<B/></td></TR>";
                msg += $"<TR><TD>Age Group:</td><td>{ageGroup}</td></TR>";
                msg += $"<TR><TD>Dose 1:</td><td>{session.available_capacity_dose1}</td></TR>";
                msg += $"<TR><TD>Dose 2:</td><td>{session.available_capacity_dose2}</td></TR>";
                msg += $"<TR><TD>Center:</td><td><B>{center.name}</B></td></TR>";
                msg += $"<TR><TD>Address:</td><td>{center.address}</td></TR>";
                msg += $"<TR><TD>Block:</td><td>{center.block_name}</td></TR>";
                msg += $"<TR><TD>PinCode:</td><td><B>{center.pincode}</B></td></TR>";
                msg += $"<TR><TD>FeeType:</td><td>{center.fee_type}</td></TR>";
                msg += $"<TR><TD></td><td></td></TR>";
                msg += $"<TR><TD></td><td></td></TR>";
            }
         
            msg += "</TABLE>";
            msg += "</BODY>";
            msg += "</HTML>";
        }

        private static void CreateMsgBody(int ageGroup, int doseNumber, string today, Center center, List<Session> sessions, string toMobiles)
        {
          
            foreach (var session in sessions)
            {
                string msg = "";
                msg += $"_Date:{session.date}";
                msg += $"_Age Group:{ageGroup}";
                msg += $"_Dose 1:{session.available_capacity_dose1}";
                msg += $"_Dose 2:{session.available_capacity_dose2}";
                msg += $"_Center:{center.name}";
                msg += $"_Block:{center.block_name}";
                msg += $"_PinCode:{center.pincode}";
                msg += $"_FeeType:{center.fee_type}";

                foreach (var mobileNumber in toMobiles.Split(';'))
                {
                    var client = new AmazonSimpleNotificationServiceClient(region: Amazon.RegionEndpoint.USWest2);
                    var request = new PublishRequest
                    {
                        Message = msg,
                        PhoneNumber = mobileNumber
                    };

                    try
                    {
                        var response = client.PublishAsync(request).Result;

                        Console.WriteLine("Message sent to " + mobileNumber + ":");
                        Console.WriteLine(msg);
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine("Caught exception publishing request:");
                        Console.WriteLine(ex.Message);
                    }
                }
                
            }

        }


        public static void Email(string subject, string htmlString, string email, string pwd, string toEmails)
        {
            try
            {
                MailMessage message = new MailMessage();
                SmtpClient smtp = new SmtpClient();
                message.From = new MailAddress(email);
                foreach (var emailAddress in toEmails.Split(';'))
                {
                    message.To.Add(new MailAddress(emailAddress));
                }

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
