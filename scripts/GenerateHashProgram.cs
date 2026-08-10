using System;
using BCrypt.Net;

class Program
{
    static void Main()
    {
        var password = "password";
        var hash = BCrypt.Net.BCrypt.HashPassword(password);
        Console.WriteLine(hash);
    }
}



