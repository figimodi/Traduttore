using System;
using System.Collections;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Windows;
using System.Windows.Input;

/*
 *          Progetto: Client traduttore WPF
 * 
 *          Autore: Filippo Greco
 *          Ultima modifica: 12/10/2018
 *  
 *          Descrizione: Applicazione WPF con interfaccia grafica per utilizzare il servizio
 *                      di traduzione online.
 *      
 *          Il servizio di traduzione, erogato dal server testlabsis.fauser.edu, è attivo sulla porta TCP 91.
 *                   
 *          A livello applicativo, il client invia una richiesta in formato testuale contenente la
 *          frase da tradurre e un insieme di informazioni di supporto secondo il seguente formato:
 *                   
 *          lingua1/frase1/lingua2<EOL>
 *                   
 *          in cui:
 *                       lingua1: è il codice ISO 639-1 della lingua in cui è stata scritta la frase da tradurre
 *                       frase1: è la frase oggetto della traduzione
 *                       lingua2: è il codice ISO 639-1 della lingua in cui sarà riportata la traduzione
 *                   
 *          Non tutte le lingue sono supportate, per sapere quali lingue sono supportate aprire il file CSV
 *          all'interno del progetto
 *               
 *          I dati della richiesta sono indicati su un'unica linea e mantenuti separati dal carattere '/'. 
 *          La linea deve terminare con il terminatore EOL (End Of Line, corrispondente a \n).
 *                   
 *          Il server, ricevuta la richiesta, esegue la traduzione e restituisce il risultato al
 *          client secondo il seguente formato:
 *                   
 *          lingua1/frase1/lingua2/frase2<EOL>
 *                   
 *          dove "frase2" rappresenta il risultato della traduzione nella lingua richiesta.
 */

namespace Traduttore
{
    public partial class MainWindow : Window
    {
        new Hashtable Language = new Hashtable();
        const int portaServer = 91;
        const string nomeServer = "eclipse.hopto.org";

        /* Il main si occupa solo di richiamare il metodo GetLanguage()*/
        public MainWindow()
        {
            InitializeComponent();
            GetLanguage();
        }




        /*  Metodo che permette di compilare il dizionario HashTable che avrà come campi:
         *  
         *  1: Come Key il nome della lingua in italiano
         *  2: Come Value la codifica ISO 639-1 della lingua
         *  
         *  Il dizionario viene creato per popolare il menù che permette di scegliere la lingua
         *  e per poi riutilizzarlo quando bisogna inviare la richiesta al server specificando
         *  la codifica ISO 639-1 ricevendo da input soltanto il nome della lingua
         *  
         *  Il dizionario viene popolato raccogliendo le informazioni da un file CSV creato in
         *  excel e salvato nella cartella radice della soluzione
        */
        private void GetLanguage()
        {
            string line;
            string[] elements;

            try
            {
                using (StreamReader reader = new StreamReader(@"..\..\Language.csv"))
                {
                    while (!reader.EndOfStream)
                    {
                        line = reader.ReadLine();
                        elements = line.Split(';');
                        LanguageIn.Items.Add(elements[0]);
                        LanguageOut.Items.Add(elements[0]);
                        Language.Add(elements[0], elements[1]);
                    }
                }
            }
            catch (Exception e) {
                txtError.Text += "Errore: " + e.Message + " Contattare lo sviluppatore" + Environment.NewLine;
            }
        }




        /*  Questo metodo viene eseguito quando il programma capisce che l'utente ha finito
         *  di digitare il testo da tradurre e può procedere con l'invio dei dati al server 
         *  utilizzando l'apposito socket e aspettando una risposta del server contenente la traduzione.
         *  Ricevuti i dati li elabora e li rilascia in output
        */
        private void Invia()
        {
            byte[] datiTx;
            byte[] datiRx = new byte[1024];
            int ricevuti;

            try
            {
                IPAddress[] indirizziIp = Dns.GetHostAddresses(nomeServer);
                if (txtError.LineCount > 6)
                    txtError.LineDown();
                if (indirizziIp.Count() < 1)
                    txtError.Text += "Errore: impossibile risolvere il nome del server: " + nomeServer + ", contattare lo sviluppatore" + Environment.NewLine;
                if (ValidazioneTesto()) return;
                IPEndPoint serverEP = new IPEndPoint(indirizziIp[0], portaServer);
                Socket client = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                client.Connect(serverEP);
                txtError.Text += "Successo: utilizzata una connessione TCP tra " + client.LocalEndPoint + " e " + client.RemoteEndPoint + Environment.NewLine;
                datiTx = Encoding.UTF8.GetBytes(Richiesta(LanguageIn.Text, LanguageOut.Text, txtIn.Text));
                client.Send(datiTx);
                ricevuti = client.Receive(datiRx);
                if (ricevuti > 0)
                {
                    string risposta = Encoding.UTF8.GetString(datiRx, 0, ricevuti);
                    txtOut.Text = ModRisposta(risposta);
                }
                client.Close();
            }
            catch (Exception e)
            {
                txtError.Text += "Errore: " + e.Message + ". Contattare lo sviluppatore" + Environment.NewLine;
            }
        }




        /*  Metodo che permette di verificare se la sintassi della frase introdotta dall'utente 
         *  rispetta le regole stabilite dal server:
         *  
         *  1: La frase non deve contenere '/'
         *  2: La frase non deve essere più lunga di 57 caratteri perchè convertendo la stringa in byte in
         *      UTF-8 vengono aggiunti altri 7 caratteri che comprendono anche la Environment.NewLine()
         *      e di conseguenza 57 + 7 = 64, numero di byte limite che il server accetta
        */
        private bool ValidazioneTesto()
        {
            if (txtIn.Text.Trim() == "") return true;
            if (txtIn.Text.Contains("/"))
            {
                txtError.Text += "Errore: la frase non può contenere il carattere '/'" + Environment.NewLine;
                txtOut.Text = "";
                return true;
            }
            if (txtIn.Text.Length > 57)
            {
                txtError.Text += "Errore: la frase da tradurre è troppo lunga!" + Environment.NewLine;
                txtOut.Text = "";
                return true;
            }
            return false;
        }




        /*  Metodo che permette di formulare la sintassi corretta da inviare al server
         *  ovvero:
         *  
         *          ISO 639-1 lingua input/testo da tradurre/ISO 639-1 lingua output
         *          
         *  Il metodo utilizza l'HashTable creata in precedenza per ricavare le codifiche
         *  ISO 639-1(value) cercando il corrispondente nome della lingua(key).
         *  Dopodiche ritorna la concatenazione delle informazioni da inviare al server.
        */
        private string Richiesta(string linguaIn, string linguaOut, string daTradurre)
        {
            string a = Language[linguaIn].ToString();
            string b = Language[linguaOut].ToString();
            return a + "/" + daTradurre + "/" + b + Environment.NewLine;
        }




        /*  Questo metodo permette di estrarre la traduzione dai dati ricevuti dal server.
         *  Scartando la parte di stringa iniziale contenenti informazioni riguardanti la
         *  lingua scelta in input, il testo da tradurre e la lingua scelta in output.
         *  Effettuando lo split si ottiene il seguente array di stringhe:
         *  sub[0] = ISO 639-1 lingua input
         *  sub[1] = testo da tradurre
         *  sub[2] = ISO 639-1 lingua output
         *  sub[3] = testo tradotto
         *  Il metodo ritorna il valore di sub[4 - 1], ovvero sub[3], ovvero il testo tradotto
        */
        static string ModRisposta(string s)
        {
            string[] sub = s.Split('/');
            return sub[sub.Length - 1];
        }




        /*  Evento del bottone che si trova tra il menù della lingua in input e il menù
         *  della lingua in output. 
         *  Esso permette di scambiare la lingua scelta in input con quella scelta in
         *  output soltanto con un click.
         *  Il funzionamento consiste nello scambiare gli indici dei due menù che sono
         *  numerati in egual modo.
        */
        private void ChangeLanguage(object sender, RoutedEventArgs e)
        {
            int temp = LanguageIn.SelectedIndex;
            LanguageIn.SelectedIndex = LanguageOut.SelectedIndex;
            LanguageOut.SelectedIndex = temp;
        }




        /*  Eventi della TextBox di input:
         * 
         *  1: TxtIn_GotFocus       ===> L'evento viene richiamato quando la TextBox viene selezionata 
         *  2: TxtIn_LostFocus      ===> L'evento viene richiamato quando la TextBox non è più selezionata
         *  3: TxtIn_KeyDown        ===> L'evento viene richiamato ad ogni tasto premuto mentre la TextBox è selezionata,
         *                                  ma il metodo Invia() viene eseguito soltanto quando viene premuto Invio (Key.Return)
        */
        private void TxtIn_GotFocus(object sender, RoutedEventArgs e)
        {
            txtIn.Text = "";
        }

        private void TxtIn_LostFocus(object sender, RoutedEventArgs e)
        {
            if (txtIn.Text.Trim() == "")
                txtIn.Text = "Inserire il testo da tradurre";
        }

        private void TxtIn_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Return) {
                txtIn.Text = txtIn.Text.Trim();
                Invia();
            }          
        }
    }
}