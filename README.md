# Banking_Accounts_Credibility
Parsing valid accounts with amounts from different currencies to EUR  into SQL Database File
# Installation
[Install .NET Core](https://docs.microsoft.com/en-us/dotnet/core/install/linux-package-manager-ubuntu-1904)

[Install Visual Studio Code](https://code.visualstudio.com/) with C# addon
# Explanation
The purpose of this program is to parsing payment file _PF.csv_ into a SQLite Database.

The Database has the following features 
![db](https://user-images.githubusercontent.com/42965639/71018451-2f45aa00-2101-11ea-8c74-72552cd1b7f9.png)

The columns *BALANCE_CURRENCY* and *PAY_AMOUNT_CURRENCY* are evaluated from *BALANCE* and *PAY_AMOUNT* columns respectively, by converting them to EUR through [Exchage Rate API](https://exchangeratesapi.io/).

The entries that are rejected from Database,through _validate_check_ function, are in _bad.csv_ file.

Finally, the program must print the total payments for every currency in EUR. 
