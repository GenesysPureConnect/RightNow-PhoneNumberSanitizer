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
        const int PageSize = 100;

        static void Main(string[] args)
        {
            Console.Clear();

            if (args == null || args.Length < 3)
            {
                Console.WriteLine("usage is ");
                Console.WriteLine();
                Console.WriteLine("RightNowPhoneNumberSanitizer.exe RIGHTNOWURL USERNAME PASSWORD");
                Console.WriteLine();
                Console.WriteLine("RIGHTNOWURL should be in the format https://inindev0513.rightnowdemo.com/cgi-bin/inindev0513.cfg/services/soap");
                Console.WriteLine("User name and password are for a user that has rights to update records in RightNow");
                Console.WriteLine();
            }
            else
            {
                try
                {
                    s_clientInfoHeader = new ClientInfoHeader();
                    s_clientInfoHeader.AppID = "Phone Number Sanitizer";

                    var client = SetupConnection(args[0], args[1], args[2]);
                    GetAllContacts(client);
                    Console.ReadLine();
                }
                catch (Exception ex)
                {
                    Console.WriteLine("ERROR: " + ex.Message);
                }
            }

            Console.WriteLine();
            Console.WriteLine("Press any key to quit...");
            Console.Read();
        }

    

        static void GetAllContacts(RightNowSyncPortClient client)
        {
            Contact contactTemplate = new Contact();
            contactTemplate.Phones = new Phone[] { };

            UpdateProcessingOptions options = new UpdateProcessingOptions();

            RNObject[] objectTemplates = new RNObject[] { contactTemplate };
            bool hasMoreResults = true;

            int currentOffset = 0;

            do
            {
                var resultTable = client.QueryObjects(s_clientInfoHeader, String.Format("SELECT Contact FROM Contact LIMIT {0} OFFSET {1}", PageSize, currentOffset), objectTemplates, PageSize);

                RNObject[] rnObjects = resultTable[0].RNObjectsResult;

                List<RNObject> updatedObjects = new List<RNObject>();

                foreach (RNObject obj in rnObjects)
                {
                    Contact contact = (Contact)obj;
                    Phone[] phones = contact.Phones;
                    if (phones != null)
                    {
                        List<Phone> newPhoneNumbers = new List<Phone>();

                        foreach (Phone phone in phones)
                        {
                            var sanitizedNumber = SanitizeNumber(phone.Number);

                            System.Console.WriteLine(contact.Name.Last + " - " + phone.Number + " (" + phone.RawNumber +") - " + sanitizedNumber);
                            if (sanitizedNumber != phone.Number)
                            {
                                //need to create a new Phone object, if we reuse/update the existing one, the update won't work. 
                                var newNumber = new Phone()
                                {
                                    action = ActionEnum.update,
                                    actionSpecified = true,
                                    Number = SanitizeNumber(phone.Number),
                                    PhoneType = phone.PhoneType
                                };
                                newPhoneNumbers.Add(newNumber);
                            }
                        }
                        if (newPhoneNumbers.Count > 0)
                        {
                            updatedObjects.Add(new Contact()
                            {
                                ID = contact.ID,
                                Phones = newPhoneNumbers.ToArray(),
                            });
                        }
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

        private static string SanitizeNumber(string number)
        {
            if (String.IsNullOrEmpty(number))
            {
                return number;
            }
            return Regex.Replace(number, "\\D", String.Empty);
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
