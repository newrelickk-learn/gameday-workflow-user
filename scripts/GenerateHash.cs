using System;
using BCrypt.Net;

class Program
{
    static void Main()
    {
        var password = "password";
        var hash = BCrypt.Net.BCrypt.HashPassword(password);
        Console.WriteLine($"Generated BCrypt hash: {hash}");
        Console.WriteLine($"Use this value to replace PLACEHOLDER_FOR_BCRYPT_HASH in seed_data.sql");
    }
}



