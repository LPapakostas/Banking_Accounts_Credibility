using System;
using System.IO;
using System.Net;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.Data.SQLite;

namespace example
{
    class Customer{
        int case_id;
        string time_stamp = DateTime.Today.ToString("yyyy-MM-dd");
        string account_number; string description;
        string pay_date; decimal pay_amount;
        decimal balance; string currency;
        decimal balance_currency; decimal pay_amount_currency;

        public Customer(int cid , string ac , string desc , string pd , decimal pa , 
                        decimal bal , string cur){     
            case_id = cid;       
            account_number = ac; description = desc ;
            pay_date = pd; pay_amount = pa;
            balance = bal; currency = cur;       
        }

        public int Case_ID{ get => case_id ; set => case_id = value;}
        public string Time_Stamp{get => time_stamp;}
        public string Account_Number{ get => account_number ; set => account_number = value;}
        public string Description{ get => description; set => description = value;}
        public string Pay_Date{ get => pay_date; set => pay_date = value;}
        public decimal Pay_Amount{ get => pay_amount; set => pay_amount = value;}
        public decimal Balance{ get => balance; set => balance = value;}
        public string Currency{ get => currency; set => currency = value;}
        public decimal Balance_Currency{ get => balance_currency; set => balance_currency = value;}
        public decimal Pay_Amount_Currency{ get => pay_amount_currency; set => pay_amount_currency = value;}
    }

    class API_Get{
        public Dictionary<string,decimal> Rates{get;set;}
    }
    class Program{
        static void Main(){
            // get data path
            string pfCSV_loc = "PF.csv" ;
            // read files from PF.csv
            string[,] values = readCSV(pfCSV_loc);
            // allocate a List for .csv data 
            List<Customer> customer_list = new List<Customer>();
            int customer_count = values.GetUpperBound(0);
            // Create a <Customer> object for every line of .csv file
            for (int i= 1; i < customer_count+1 ; i++ ){
                Customer c = new Customer(i,values[i,0],values[i,1],values[i,2],
                                 Convert.ToDecimal(values[i,3]),Convert.ToDecimal(values[i,4]),
                                 values[i,5]);
                customer_list.Add(c);
            }
            // find actual date of bank exchanges
            string api_payday = (customer_list[0]).Pay_Date;
            //get API with EUR as reference for exchange date
            string exchange_url = String.Format("https://api.exchangeratesapi.io/"+api_payday);
            // save API Data into a dictionary <key : Currency Code> <value : rate>
            Dictionary<string,decimal> global_rates = getAPIexchagerates(exchange_url);
            // Prellocate valid customer list that will inserted on SQLite Database
            List<Customer> valid_customers = new List<Customer>();
            string file_path = "bad.csv";
            // Delete file each time in order to avoid name conflicts
            if(File.Exists(file_path)){
                File.Delete(file_path);
            }
            // Validate if our data is ready for parsing in database
            foreach(Customer c in customer_list ){
                if (validate_check(c,global_rates)){
                    valid_customers.Add(c);
                }
                else{
                    writeCSV(c,file_path);
                }
            }
            //print summaries of payments
            printPaymentSums(global_rates,valid_customers);
            
            // Create SQLite Database
            string CreateTableQuery = "CREATE TABLE IF NOT EXISTS[MY_PAYMENTS]([CASE_ID] NUMBER ,[TIME_STAMP] DATE ,[ACCOUNT] NUMBER ,[DESCRIPTION] TEXT(50),[PAY_DATE] DATE ,[PAY_AMOUNT] NUMBER(10,2) ,[BALANCE] NUMBER(10,2) ,[CURRENCY] TEXT(3) , [BALANCE_CURRENCY] NUMBER(10,2) , [PAY_AMOUNT_CURRENCY] NUMBER(10,2) )";
            string DatabaseFile = "my_payments.db";
            string DatabaseSource = "Data Source="+DatabaseFile;
            
            // Each time , we delete the previous database in order to avoid conflicts
            if (File.Exists(DatabaseFile))
            {
                File.Delete(DatabaseFile);
                SQLiteConnection.CreateFile(DatabaseFile);
            }
            
            SQLiteConnection sqlite_conn = new SQLiteConnection(DatabaseSource);
            sqlite_conn.Open();
            SQLiteCommand sqlite_cmd = sqlite_conn.CreateCommand();
            sqlite_cmd.CommandText = CreateTableQuery;
            sqlite_cmd.ExecuteNonQuery();
            
            foreach(Customer temp_c in valid_customers){
                string t_id = temp_c.Case_ID.ToString(); 
                string t_ts = temp_c.Time_Stamp; 
                string t_acc = temp_c.Account_Number.ToString(); 
                string t_desc = temp_c.Description; 
                string t_pd = temp_c.Pay_Date; 
                string t_pa = temp_c.Pay_Amount.ToString();
                string t_bal = temp_c.Balance.ToString(); 
                string t_cur = temp_c.Currency;
                string t_bal_c = temp_c.Balance_Currency.ToString();
                string t_pa_c = temp_c.Pay_Amount_Currency.ToString();
               
                sqlite_cmd.CommandText = @"INSERT INTO MY_PAYMENTS (CASE_ID,TIME_STAMP,ACCOUNT,DESCRIPTION,PAY_DATE
                                    ,PAY_AMOUNT,BALANCE,CURRENCY,BALANCE_CURRENCY,PAY_AMOUNT_CURRENCY) VALUES ('" + t_id + "','" + t_ts + "','" +
                                    t_acc + "','" + t_desc +"','"+ t_pd +"','" + t_pa + "','" + t_bal + "','"
                                    + t_cur + "','" + t_bal_c + "','" + t_pa_c+"')"; 
                sqlite_cmd.ExecuteNonQuery();
            }
            
            Console.WriteLine("FINISHED");            
        }

        static string[,] readCSV(string filename){
            // read .csv file and convert to a string one
            string whole_file = System.IO.File.ReadAllText(filename);
            // split file into lines
            whole_file = whole_file.Replace('\n','\r');
            string[] lines = whole_file.Split(new char[] {'\r'},StringSplitOptions.RemoveEmptyEntries);
            // find rows and columns for all data 
            int rows = lines.Length ;
            int columns = lines[0].Split(';').Length;
            // allocate data array
            string[,] values = new string[rows,columns];
            // fill empty array of values with actual values of .csv file
            for (int i =0 ; i < rows; i++ ){
                string[] temp_line = lines[i].Split(';');
                for (int j=0; j< columns ; j++){
                    values[i,j] = temp_line[j];
                }
            }
            return values;
        }

        static Dictionary<string,decimal> getAPIexchagerates(string url){
            // Fetching data from web
            WebRequest api_request = WebRequest.Create(url);
            api_request.Method = "GET";
            HttpWebResponse api_response = null;
            api_response = (HttpWebResponse)api_request.GetResponse();

            string exchange_values = null;
            using( Stream stream = api_response.GetResponseStream() ){
                StreamReader sr = new StreamReader(stream);
                exchange_values = sr.ReadToEnd();
                sr.Close();
            }
            // json file to an object
            API_Get g_rates = JsonConvert.DeserializeObject<API_Get>(exchange_values);
            // We append EUR currency == 1 so that dictionary has all currency values
            Dictionary<string,decimal> global_rates = g_rates.Rates;
            global_rates.Add("EUR",(decimal)1.00);
            return global_rates;
        }
        
        static bool validate_check(Customer c,Dictionary<string,decimal> gr){
            // Check if Account_Number is a long NUMBER
            bool acc_check = long.TryParse(c.Account_Number,out long n);
            if (!acc_check){
                return false;
            }
            // Check if DESCRIPTION length is less than 50
            if(c.Description.Length > 50){
                return false;
            }
            // Check if PAY_DATE is existing
            if(c.Pay_Date == null){
                return false;
            }
            // Check if PAY_AMOUNT has less than 10 digits before {.} and exactly 2 digits after {.}
            string[] pay_amount_validate = (""+c.Pay_Amount).Split(new char[] {'.'});
            if(pay_amount_validate[0].Length > 10 || pay_amount_validate[1].Length !=2 ){
                return false;
            }
            // Check if BALANCE has less than 10 digits before {.} and exactly 2 digits after {.}
            string[] balance_validate = (""+c.Balance).Split(new char[] {'.'});
            if(balance_validate[0].Length > 10 || balance_validate[1].Length !=2 ){
                return false;
            }
            // Check if CURRENCY is in API Database
            if(!gr.ContainsKey(c.Currency)){
                return false;
            }
            // After we checked about currency, we can now compute balance and pay_amount in EUR
            decimal f = gr[c.Currency];
            c.Balance_Currency = ((decimal)1.0/f)*c.Balance;
            c.Pay_Amount_Currency = ((decimal)1.0/f)*c.Pay_Amount;
            // Compute BALANCE_CURRENCY and check if it has less 
            // than 10 digits before {.} and exactly 2 digits after {.}.
            // If not , we round at 2 digits.
            string[] balance_c_validate = (""+c.Balance_Currency).Split(new char[] {'.'});
            if(balance_c_validate[0].Length > 10 || balance_c_validate[1].Length !=2 ){
                string round_deci_b = balance_c_validate[1].Substring(0,2);
                c.Balance_Currency = Convert.ToDecimal(balance_c_validate[0]+"."+round_deci_b);
            }
            // Same precedure as BALANCE_CURRENCY
            string[] pay_amount_c_validate = (""+c.Pay_Amount_Currency).Split(new char[] {'.'});
            if(pay_amount_c_validate[0].Length > 10 || pay_amount_c_validate[1].Length !=2 ){
                string round_deci_pa = pay_amount_c_validate[1].Substring(0,2);
                c.Pay_Amount_Currency = Convert.ToDecimal(pay_amount_c_validate[0]+"."+round_deci_pa);
            }
            return true;
        }

        static void writeCSV(Customer c, string filename_path){
            using (StreamWriter file = new StreamWriter(filename_path,true) ){
                file.WriteLine(c.Case_ID + ";"+ c.Time_Stamp+";"+c.Account_Number+";"+c.Description
                +";"+c.Pay_Date+";"+c.Pay_Amount+";"+c.Balance+";"+c.Currency);
            }
        }

        static void printPaymentSums(Dictionary<string,decimal> g_rates , List<Customer> customer_lst ){
            // Create an empty dictionary that contains country codes and zeros. Zeros express the 
            // initial payment summary in euros for each country code.
            Dictionary<string,decimal> summaries_in_Euros = new Dictionary<string, decimal>(); 
            var temp_list = new List<string>(g_rates.Keys);
            foreach(string x in temp_list){
                summaries_in_Euros.Add(x,0);
            }
            foreach(Customer c_temp in customer_lst){
                // Find currency factor 
                decimal factor = g_rates[c_temp.Currency];
                decimal payment = c_temp.Pay_Amount;
                summaries_in_Euros[c_temp.Currency]+= (1/factor)*payment;
            }
            // Remove Currencies that are not appeared in csv file
            foreach(var pair in summaries_in_Euros){
                 if (pair.Value == 0){
                    summaries_in_Euros.Remove(pair.Key);
                }
            }
            Console.WriteLine("Summaries of All Currencies that presented in valid data(EUR)");
            foreach(var pair in summaries_in_Euros){
                Console.WriteLine("{0}:{1}",pair.Key,pair.Value);
            }

        }
    }
}
