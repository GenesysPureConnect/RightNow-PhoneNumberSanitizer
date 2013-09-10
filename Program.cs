using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.ServiceModel;
using RightNowPhoneNumberSanitizer.RightNow.Soap;
using System.ServiceModel.Channels;
using System.Text.RegularExpressions;

namespace RightNowPhoneNumberSanitizer
{
    class Program
    {
        private static ClientInfoHeader s_clientInfoHeader;
        private static bool s_trimLeadingOne = true;

        static void Main(string[] args)
        {
            s_clientInfoHeader = new ClientInfoHeader();
            s_clientInfoHeader.AppID = "Phone Number Sanitizer";

            var client = SetupConnection("https://inindev0513.rightnowdemo.com/cgi-bin/inindev0513.cfg/services/soap", "Admin1", "Admin1234");
            GetAllContacts(client);

        }

    

        static void GetAllContacts(RightNowSyncPortClient client)
        {
            Contact contactTemplate = new Contact();
            contactTemplate.Phones = new Phone[] { };

            UpdateProcessingOptions options = new UpdateProcessingOptions();
           // options.SuppressExternalEvents = true;
            //options.SuppressRules = true;

            RNObject[] objectTemplates = new RNObject[] { contactTemplate };
            bool hasMoreResults = true;

            const int PageSize = 100;
            int currentOffset = 0;

            do
            {
               // var resultTable = client.QueryObjects(s_clientInfoHeader, String.Format("SELECT Contact FROM Contact LIMIT {0} OFFSET {1}", PageSize, currentOffset), objectTemplates, PageSize);
                var resultTable = client.QueryObjects(s_clientInfoHeader, String.Format("SELECT Contact FROM Contact C WHERE C.Phones.RawNumber LIKE '%{0}'", "9154929469"), objectTemplates, PageSize);

                RNObject[] rnObjects = resultTable[0].RNObjectsResult;

                List<RNObject> updatedObjects = new List<RNObject>();

                foreach (RNObject obj in rnObjects)
                {
                    Contact contact = (Contact)obj;
                    Phone[] phones = contact.Phones;
                    if (phones != null)
                    {
                        foreach (Phone phone in phones)
                        {
                            System.Console.WriteLine(contact.Name.Last + " - " + phone.Number + "(" + phone.RawNumber +") - " + SanitizeNumber(phone.Number));
                            phone.Number = SanitizeNumber(phone.Number);
                            phone.RawNumber = phone.Number;
                        }

                        updatedObjects.Add(new Contact()
                        {
                            ID = contact.ID,
                            Phones = phones
                        });
                    }
                }

                if (updatedObjects.Count > 0)
                {
                    client.Update(s_clientInfoHeader, updatedObjects.ToArray(), options);
                }

                hasMoreResults = resultTable[0].Paging.ReturnedCount == PageSize;
                currentOffset = currentOffset + resultTable[0].Paging.ReturnedCount;

                Console.WriteLine(String.Format("Processed {0} contacts", currentOffset));
            } while (hasMoreResults);
        }

        static string SanitizeNumber(string number)
        {
            var newNumber = Regex.Replace(number,@"\D",String.Empty);

            if (s_trimLeadingOne && newNumber.StartsWith("1") && newNumber.Length == 11)
            {
                newNumber = newNumber.Substring(1);
            }

            return newNumber;
        }

        static RightNowSyncPortClient SetupConnection(string endpointAddress, string user, string password)
        {
            BasicHttpBinding binding = new BasicHttpBinding(BasicHttpSecurityMode.TransportWithMessageCredential);
            binding.MaxBufferPoolSize = 20000000;
            binding.MaxBufferSize = 20000000;
            binding.MaxReceivedMessageSize = 20000000;
            binding.Security.Message.ClientCredentialType = BasicHttpMessageCredentialType.UserName;
            EndpointAddress endPointAddress = new EndpointAddress(endpointAddress);

            var service = new RightNowSyncPortClient(binding, endPointAddress);
            service.ClientCredentials.UserName.UserName = user;
            service.ClientCredentials.UserName.Password = password;

            BindingElementCollection elements = service.Endpoint.Binding.CreateBindingElements();
            elements.Find<SecurityBindingElement>().IncludeTimestamp = false;
            service.Endpoint.Binding = new CustomBinding(elements);

            return service;
        }
    }
}
