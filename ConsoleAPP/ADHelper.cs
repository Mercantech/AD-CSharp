using System;
using System.Collections.Generic;
using System.DirectoryServices.Protocols;
using System.IO;
using System.Linq;
using System.Net;
using System.Text.Json;

namespace ConsoleAPP
{
    public class ADHelper
    {
        private static string _server = "10.133.71.100";
        private static string _username = "adReader";
        private static string _password = "Merc1234!";
        private static string _domain = "mags.local";

        private static string _AdminUsername = "CRUD";
        private static string _AdminPassword = ")e=4To!3@(SKLnCPWLz'[8";

        private static LdapConnection GetConnection()
        {
            try
            {
                var credential = new NetworkCredential($"{_username}@{_domain}", _password);
                return new LdapConnection(_server)
                {
                    Credential = credential,
                    AuthType = AuthType.Negotiate
                };
            }
            catch (Exception ex)
            {
                throw new Exception($"Fejl ved oprettelse af LDAP-forbindelse: {ex.Message}");
            }
        }

        public static string GetServer()
        {
            try
            {
                using (var connection = GetConnection())
                {
                    connection.Bind();
                    return "Kørende";
                }
            }
            catch
            {
                return "Ikke kørende";
            }
        }

        public static List<ADGroup> GetAllGroups()
        {
            // Opret en tom liste til at gemme alle AD grupper
            var groups = new List<ADGroup>();

            // Opret forbindelse til Active Directory
            using (var connection = GetConnection())
            {
                // Definer søgningen:
                var searchRequest = new SearchRequest(
                    "DC=mags,DC=local",
                    "(objectClass=group)",
                    SearchScope.Subtree,
                    "*" // Hent alle attributter ved at bruge "*"
                );

                try
                {
                    // Udfør søgningen
                    var response = (SearchResponse)connection.SendRequest(searchRequest);

                    // Gem den rå response som JSON med alle attributter
                    var rawGroups = response
                        .Entries.Cast<SearchResultEntry>()
                        .Select(entry =>
                        {
                            var attributes = new Dictionary<string, List<string>>();
                            foreach (string attrName in entry.Attributes.AttributeNames)
                            {
                                var values = entry
                                    .Attributes[attrName]
                                    .GetValues(typeof(string))
                                    .Cast<string>()
                                    .ToList();
                                attributes[attrName] = values;
                            }
                            return new
                            {
                                DistinguishedName = entry.DistinguishedName,
                                Attributes = attributes
                            };
                        })
                        .ToList();

                    SaveResponseAsJson(
                        new
                        {
                            ResultCode = response.ResultCode.ToString(),
                            MatchedDN = response.MatchedDN,
                            EntryCount = response.Entries.Count,
                            Groups = rawGroups
                        },
                        "raw_groups_response.json"
                    );

                    // For hver gruppe vi finder
                    foreach (SearchResultEntry gruppe in response.Entries)
                    {
                        // Opret et nyt ADGroup objekt med informationerne
                        var nyGruppe = new ADGroup
                        {
                            // Hvis værdien ikke findes, brug "N/A" som standard
                            Name = gruppe.Attributes["cn"]?[0]?.ToString() ?? "N/A",
                            Description = gruppe.Attributes["description"]?[0]?.ToString() ?? "N/A"
                        };

                        // Tilføj gruppen til vores liste
                        groups.Add(nyGruppe);
                    }

                    // Gem den behandlede liste af grupper som JSON
                    SaveResponseAsJson(groups, "processed_groups.json");
                }
                catch (Exception ex)
                {
                    // Hvis noget går galt, fortæl hvad der skete
                    throw new Exception($"Der skete en fejl ved hentning af grupper: {ex.Message}");
                }
            }

            // Send alle de fundne grupper tilbage
            return groups;
        }

        public static List<ADUser> GetAllUsers()
        {
            var users = new List<ADUser>();
            using (var connection = GetConnection())
            {
                var searchRequest = new SearchRequest(
                    "DC=mags,DC=local",
                    "(objectClass=user)",
                    SearchScope.Subtree,
                    "cn",
                    "samAccountName",
                    "mail",
                    "department",
                    "title",
                    "distinguishedName"
                );

                try
                {
                    var response = (SearchResponse)connection.SendRequest(searchRequest);
                    foreach (SearchResultEntry entry in response.Entries)
                    {
                        users.Add(
                            new ADUser
                            {
                                Name = entry.Attributes["cn"]?[0]?.ToString() ?? "N/A",
                                Username =
                                    entry.Attributes["samAccountName"]?[0]?.ToString() ?? "N/A",
                                Email = entry.Attributes["mail"]?[0]?.ToString() ?? "N/A",
                                Department =
                                    entry.Attributes["department"]?[0]?.ToString() ?? "N/A",
                                Title = entry.Attributes["title"]?[0]?.ToString() ?? "N/A",
                                DistinguishedName =
                                    entry.Attributes["distinguishedName"]?[0]?.ToString() ?? "N/A"
                            }
                        );
                    }

                    // Gem resultatet som JSON
                    SaveResponseAsJson(users, "users.json");
                }
                catch (Exception ex)
                {
                    throw new Exception($"Fejl ved hentning af brugere: {ex.Message}");
                }
            }
            return users;
        }

        public static Dictionary<string, List<ADUser>> GetGroupsWithMembers()
        {
            var groupMembers = new Dictionary<string, List<ADUser>>();
            using (var connection = GetConnection())
            {
                // Først henter vi alle grupper
                var groupSearchRequest = new SearchRequest(
                    "DC=mags,DC=local",
                    "(objectClass=group)",
                    SearchScope.Subtree,
                    "cn",
                    "member",
                    "distinguishedName"
                );

                try
                {
                    var groupResponse = (SearchResponse)connection.SendRequest(groupSearchRequest);
                    Console.WriteLine($"Fundet {groupResponse.Entries.Count} grupper");
                    foreach (SearchResultEntry groupEntry in groupResponse.Entries)
                    {
                        var groupName = groupEntry.Attributes["cn"][0].ToString();
                        var memberCount = groupEntry.Attributes.Contains("member")
                            ? groupEntry.Attributes["member"].Count
                            : 0;
                        Console.WriteLine($"Gruppe {groupName} har {memberCount} medlemmer");
                        var members = new List<ADUser>();

                        // Tjek om gruppen har medlemmer
                        if (groupEntry.Attributes.Contains("member"))
                        {
                            foreach (
                                var memberDN in groupEntry
                                    .Attributes["member"]
                                    .GetValues(typeof(string))
                            )
                            {
                                // For hvert medlem, hent deres detaljer
                                var userSearchRequest = new SearchRequest(
                                    memberDN.ToString(),
                                    "(objectClass=*)",
                                    SearchScope.Base,
                                    "cn",
                                    "samAccountName",
                                    "mail",
                                    "department",
                                    "title"
                                );

                                try
                                {
                                    var userResponse = (SearchResponse)
                                        connection.SendRequest(userSearchRequest);
                                    if (userResponse.Entries.Count > 0)
                                    {
                                        var userEntry = userResponse.Entries[0];
                                        members.Add(
                                            new ADUser
                                            {
                                                Name =
                                                    userEntry.Attributes["cn"]?[0]?.ToString()
                                                    ?? "N/A",
                                                Username =
                                                    userEntry
                                                        .Attributes["samAccountName"]
                                                        ?[0]?.ToString() ?? "N/A",
                                                Email =
                                                    userEntry.Attributes["mail"]?[0]?.ToString()
                                                    ?? "N/A",
                                                Department =
                                                    userEntry
                                                        .Attributes["department"]
                                                        ?[0]?.ToString() ?? "N/A",
                                                Title =
                                                    userEntry.Attributes["title"]?[0]?.ToString()
                                                    ?? "N/A"
                                            }
                                        );
                                    }
                                }
                                catch (Exception ex)
                                {
                                    // Log eller håndter fejl for individuelle medlemmer
                                    Console.WriteLine(
                                        $"Fejl ved hentning af bruger {memberDN}: {ex.Message}"
                                    );
                                }
                            }
                        }

                        groupMembers.Add(groupName, members);
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"Fejl ved hentning af grupper og medlemmer: {ex.Message}");
                }
            }
            return groupMembers;
        }

        public static List<ADUser> SearchUsers(string searchTerm, string searchCriteria = "all")
        {
            var filter = searchCriteria.ToLower() switch
            {
                "email" => $"(&(objectClass=user)(mail=*{searchTerm}*))",
                "department" => $"(&(objectClass=user)(department=*{searchTerm}*))",
                "name" => $"(&(objectClass=user)(cn=*{searchTerm}*))",
                _
                    => $"(&(objectClass=user)(|(cn=*{searchTerm}*)(mail=*{searchTerm}*)(department=*{searchTerm}*)))"
            };

            var users = new List<ADUser>();
            using (var connection = GetConnection())
            {
                var searchRequest = new SearchRequest(
                    "DC=mags,DC=local",
                    filter,
                    SearchScope.Subtree,
                    "cn",
                    "samAccountName",
                    "mail",
                    "department",
                    "title"
                );

                try
                {
                    var response = (SearchResponse)connection.SendRequest(searchRequest);
                    foreach (SearchResultEntry entry in response.Entries)
                    {
                        users.Add(
                            new ADUser
                            {
                                Name = entry.Attributes["cn"]?[0]?.ToString() ?? "N/A",
                                Username =
                                    entry.Attributes["samAccountName"]?[0]?.ToString() ?? "N/A",
                                Email = entry.Attributes["mail"]?[0]?.ToString() ?? "N/A",
                                Department =
                                    entry.Attributes["department"]?[0]?.ToString() ?? "N/A",
                                Title = entry.Attributes["title"]?[0]?.ToString() ?? "N/A"
                            }
                        );
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"Fejl ved søgning efter brugere: {ex.Message}");
                }
            }
            return users;
        }

        public static List<ADGroup> SearchGroups(string searchTerm)
        {
            var groups = new List<ADGroup>();
            using (var connection = GetConnection())
            {
                var searchRequest = new SearchRequest(
                    "DC=mags,DC=local",
                    $"(&(objectClass=group)(cn=*{searchTerm}*))",
                    SearchScope.Subtree,
                    "cn",
                    "description",
                    "distinguishedName"
                );

                try
                {
                    var response = (SearchResponse)connection.SendRequest(searchRequest);
                    foreach (SearchResultEntry entry in response.Entries)
                    {
                        groups.Add(
                            new ADGroup
                            {
                                Name = entry.Attributes["cn"]?[0]?.ToString() ?? "N/A",
                                Description =
                                    entry.Attributes["description"]?[0]?.ToString() ?? "N/A"
                            }
                        );
                    }
                }
                catch (Exception ex)
                {
                    throw new Exception($"Fejl ved søgning efter grupper: {ex.Message}");
                }
            }
            return groups;
        }

        public static void SaveResponseAsJson<T>(T data, string fileName)
        {
            try
            {
                // Opret en dedikeret mappe til JSON-filer
                string outputDirectory = Path.Combine(
                    AppDomain.CurrentDomain.BaseDirectory,
                    "json_output"
                );
                Directory.CreateDirectory(outputDirectory);

                // Opret den fulde sti til filen
                string fullPath = Path.Combine(outputDirectory, fileName);

                var options = new JsonSerializerOptions { WriteIndented = true };
                string jsonString = JsonSerializer.Serialize(data, options);
                File.WriteAllText(fullPath, jsonString);
                Console.WriteLine($"Data gemt i: {fullPath}");
            }
            catch (Exception ex)
            {
                throw new Exception($"Fejl ved gemning af JSON: {ex.Message}");
            }
        }

        public static void CreateUser(
            string username,
            string name,
            string email,
            string department,
            string title,
            string firstName,
            string lastName,
            string initials
        )
        {
            using (var connection = new LdapConnection(_server))
            {
                var credential = new NetworkCredential(
                    $"{_AdminUsername}@{_domain}",
                    _AdminPassword
                );
                connection.Credential = credential;
                connection.AuthType = AuthType.Negotiate;
                connection.Bind();

                // Tjek om brugernavn eller navn allerede findes
                var searchRequest = new SearchRequest(
                    "CN=Users,DC=mags,DC=local",
                    $"(|(sAMAccountName={username})(cn={name}))",
                    SearchScope.OneLevel,
                    "cn",
                    "sAMAccountName"
                );
                var response = (SearchResponse)connection.SendRequest(searchRequest);
                if (response.Entries.Count > 0)
                    throw new Exception($"Brugernavn eller navn findes allerede i AD.");

                string userDn = $"CN={name},CN=Users,DC=mags,DC=local";

                var attributes = new List<DirectoryAttribute>
                {
                    new DirectoryAttribute("objectClass", "user"),
                    new DirectoryAttribute("sAMAccountName", username),
                    new DirectoryAttribute("userPrincipalName", $"{username}@{_domain}"),
                    new DirectoryAttribute("displayName", name),
                    new DirectoryAttribute("mail", email),
                    new DirectoryAttribute("department", department),
                    new DirectoryAttribute("title", title),
                    new DirectoryAttribute("givenName", firstName),
                    new DirectoryAttribute("sn", lastName),
                    new DirectoryAttribute("initials", initials)
                };

                var request = new AddRequest(userDn, attributes.ToArray());
                try
                {
                    connection.SendRequest(request);
                }
                catch (Exception ex)
                {
                    throw new Exception($"Fejl ved oprettelse af bruger: {ex.Message}");
                }
            }
        }

        public static void CreateGroup(string groupName, string description)
        {
            using (var connection = new LdapConnection(_server))
            {
                var credential = new NetworkCredential(
                    $"{_AdminUsername}@{_domain}",
                    _AdminPassword
                );
                connection.Credential = credential;
                connection.AuthType = AuthType.Negotiate;
                connection.Bind();

                // Tjek om gruppen allerede findes
                var searchRequest = new SearchRequest(
                    "CN=Users,DC=mags,DC=local",
                    $"(&(objectClass=group)(cn={groupName}))",
                    SearchScope.OneLevel
                );
                var response = (SearchResponse)connection.SendRequest(searchRequest);
                if (response.Entries.Count > 0)
                    throw new Exception($"Gruppen '{groupName}' findes allerede.");

                string groupDn = $"CN={groupName},CN=Users,DC=mags,DC=local";

                var attributes = new List<DirectoryAttribute>
                {
                    new DirectoryAttribute("objectClass", "group"),
                    new DirectoryAttribute("sAMAccountName", groupName),
                    new DirectoryAttribute("description", description)
                };

                var request = new AddRequest(groupDn, attributes.ToArray());
                try
                {
                    connection.SendRequest(request);
                }
                catch (Exception ex)
                {
                    throw new Exception($"Fejl ved oprettelse af gruppe: {ex.Message}");
                }
            }
        }

        private static byte[] EncodePassword(string password)
        {
            // AD kræver at adgangskoden er i Unicode og omsluttet af anførselstegn
            string quotedPassword = $"\"{password}\"";
            return System.Text.Encoding.Unicode.GetBytes(quotedPassword);
        }

        public static void DeleteUser(string username)
        {
            using (var connection = new LdapConnection(_server))
            {
                var credential = new NetworkCredential(
                    $"{_AdminUsername}@{_domain}",
                    _AdminPassword
                );
                connection.Credential = credential;
                connection.AuthType = AuthType.Negotiate;
                connection.Bind();

                // Find brugerens DN
                var searchRequest = new SearchRequest(
                    "CN=Users,DC=mags,DC=local",
                    $"(&(objectClass=user)(sAMAccountName={username}))",
                    SearchScope.OneLevel,
                    "distinguishedName"
                );
                var response = (SearchResponse)connection.SendRequest(searchRequest);
                if (response.Entries.Count == 0)
                    throw new Exception($"Bruger '{username}' blev ikke fundet.");

                var userDn = response.Entries[0].DistinguishedName;

                // Slet brugeren
                var deleteRequest = new DeleteRequest(userDn);
                try
                {
                    connection.SendRequest(deleteRequest);
                }
                catch (Exception ex)
                {
                    throw new Exception($"Fejl ved sletning af bruger: {ex.Message}");
                }
            }
        }
    }

    public class ADUser
    {
        public string Name { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string Department { get; set; }
        public string Title { get; set; }
        public string DistinguishedName { get; set; }
    }

    public class ADGroup
    {
        public string Name { get; set; }
        public string Description { get; set; }
    }
}
