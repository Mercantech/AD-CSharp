namespace ConsoleAPP
{
    internal class Program
    {
        static void Main(string[] args)
        {
            while (true)
            {
                Console.Clear();
                Console.WriteLine("Active Directory Administration");
                Console.WriteLine("==============================");
                Console.WriteLine($"Server Status: {ADHelper.GetServer()}\n");
                Console.WriteLine("1. Vis alle grupper");
                Console.WriteLine("2. Vis alle brugere");
                Console.WriteLine("3. Vis grupper med medlemmer");
                Console.WriteLine("4. Søg efter brugere");
                Console.WriteLine("5. Søg efter grupper");
                Console.WriteLine("6. Afslut");
                Console.Write("\nVælg en mulighed (1-6): ");

                var choice = Console.ReadLine();

                try
                {
                    switch (choice)
                    {
                        case "1":
                            ShowAllGroups();
                            break;
                        case "2":
                            ShowAllUsers();
                            break;
                        case "3":
                            ShowGroupsWithMembers();
                            break;
                        case "4":
                            SearchUsers();
                            break;
                        case "5":
                            SearchGroups();
                            break;
                        case "6":
                            return;
                        default:
                            Console.WriteLine("Ugyldigt valg. Prøv igen.");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"\nDer opstod en fejl: {ex.Message}");
                }

                Console.WriteLine("\nTryk på en tast for at fortsætte...");
                Console.ReadKey();
            }
        }

        static void ShowAllGroups()
        {
            var groups = ADHelper.GetAllGroups();
            Console.WriteLine("\nAlle grupper:");
            foreach (var group in groups)
            {
                Console.WriteLine($"Navn: {group.Name}");
                Console.WriteLine($"Beskrivelse: {group.Description}");
                Console.WriteLine("------------------------");
            }
        }

        static void ShowAllUsers()
        {
            var users = ADHelper.GetAllUsers();
            Console.WriteLine("\nAlle brugere:");
            foreach (var user in users)
            {
                Console.WriteLine($"Navn: {user.Name}");
                Console.WriteLine($"Brugernavn: {user.Username}");
                Console.WriteLine($"Email: {user.Email}");
                Console.WriteLine($"Afdeling: {user.Department}");
                Console.WriteLine($"Titel: {user.Title}");
                Console.WriteLine("------------------------");
            }
        }

        static void ShowGroupsWithMembers()
        {
            var groupMembers = ADHelper.GetGroupsWithMembers();
            Console.WriteLine("\nGrupper og deres medlemmer:");
            foreach (var group in groupMembers)
            {
                Console.WriteLine($"\nGruppe: {group.Key}");
                Console.WriteLine("Medlemmer:");
                foreach (var member in group.Value)
                {
                    Console.WriteLine($"- {member.Name} ({member.Email})");
                }
                Console.WriteLine("------------------------");
            }
        }

        static void SearchUsers()
        {
            Console.Write("\nSøg efter (navn/email/afdeling): ");
            var criteria = Console.ReadLine()?.ToLower() ?? "all";
            Console.Write("Søgeord: ");
            var searchTerm = Console.ReadLine() ?? "";

            var users = ADHelper.SearchUsers(searchTerm, criteria);
            Console.WriteLine($"\nFundne brugere ({users.Count}):");
            foreach (var user in users)
            {
                Console.WriteLine($"Navn: {user.Name}");
                Console.WriteLine($"Email: {user.Email}");
                Console.WriteLine($"Afdeling: {user.Department}");
                Console.WriteLine("------------------------");
            }
        }

        static void SearchGroups()
        {
            Console.Write("\nSøg efter gruppe: ");
            var searchTerm = Console.ReadLine() ?? "";

            var groups = ADHelper.SearchGroups(searchTerm);
            Console.WriteLine($"\nFundne grupper ({groups.Count}):");
            foreach (var group in groups)
            {
                Console.WriteLine($"Navn: {group.Name}");
                Console.WriteLine($"Beskrivelse: {group.Description}");
                Console.WriteLine("------------------------");
            }
        }
    }
}
