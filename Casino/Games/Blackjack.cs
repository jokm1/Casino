﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ConsoleTables;

namespace Casino.Games
{
    public class Blackjack : Game
    {
        public new static readonly string Name = "Blackjack";

        public new static readonly string Description = "...";

        // Menu akcii hry
        private readonly Menu.ListMenu actionsMenu = new Menu.ListMenu
        {
            Items = new List<Menu.MenuItem>()
        };

        public Blackjack()
        {
            // Vytvori menu
            foreach (var item in Enum.GetValues(typeof(Items.Player.EActions)))
            {
                actionsMenu.AddItem(new Menu.MenuItem
                {
                    Key = ConsoleKey.F1,
                    Text = item.ToString(),
                    Action = () =>
                    {
                        Program.myPlayer.Action = (Items.Player.EActions)item;
                    },
                    IsEnabled = true
                });
            }

            // Hlavicka
            Head();
        }

        // Jadro hry
        public override void Run()
        {
            // Jadro hry
            using (var client = new Client())
            {
                // Hlavna slucka hry
                while (true)
                {
                    // Hrac zada vysku stavky, s ktorou ide hrat
                    Program.myPlayer.Bet = SetBet();

                    // Ak si praje hrac skoncit
                    if (Program.myPlayer.Bet <= 0)
                        break;

                    // Ak hrac vsadi 0 konci hru, alebo ked nema dost penazi na stavky
                    if (Program.myPlayer.Wallet < Program.myPlayer.Bet)
                    {
                        Program.ShowError("You don't have enaught money for play");
                        Console.WriteLine("\n");
                        break;
                    }

                    // Nastav hraca
                    Program.myPlayer.GameId = "Blackjack";
                    Program.myPlayer.State = Items.Player.EState.PLAYING;
                    Program.myPlayer.Action = Items.Player.EActions.NONE;

                    // Novy hrac taha 2 karty
                    client.UpdateItemAsync(new Uri(Program.serverUri, "blackjack/get-card"), Program.myPlayer).Wait();
                    client.UpdateItemAsync(new Uri(Program.serverUri, "blackjack/get-card"), Program.myPlayer).Wait();

                    // Aktualizuj hraca
                    client.UpdateItemAsync(new Uri(Program.serverUri, "players/update"), Program.myPlayer).Wait();

                    // Prebieha, kym hrac neskonci rozohranu partiu
                    do
                    {
                        // Nacitaj hracov
                        var playersDb = (List<Items.Player>)client.GetListAsync(new Uri(Program.serverUri, "blackjack/players"), typeof(List<Items.Player>)).Result;
                        var croupier = playersDb.Where(p => p.Nickname == "Croupier-Blackjack").ToArray()[0];
                        var players = playersDb.Where(p => p.Nickname != "Croupier-Blackjack").ToList();

                        if (players != null && players.Count > 0 && croupier != null)
                        {
                            // Vypis herneho stola
                            Print(players, croupier);

                            // Ak nie je hrac aktivny ukonci kolo
                            if (Program.myPlayer.State != Items.Player.EState.PLAYING)
                                break;

                            // Moj hrac
                            var my = players.Where(p => p.Nickname == Program.myPlayer.Nickname)
                                            .Select(p => new { p.CardSum, CardCount = p.Cards.Count() }).ToArray();
                            var mySum = my[0].CardSum;

                            // Ak hrac este neukoncil hru a vybera si akcie
                            if (Program.myPlayer.Action != Items.Player.EActions.STAND)
                            {
                                // Prekrocil 21 prehral
                                if (mySum > 21)
                                {
                                    // Nastav prehru hraca
                                    Program.myPlayer.State = Items.Player.EState.LOSE;
                        
                                    // Prisiel o stavku
                                    Program.myPlayer.Wallet -= Program.myPlayer.Bet;
                                }
                                // Hrac ziskal prave 21 bodov - ukoncil hru
                                else if ((mySum == 21) || (Program.myPlayer.Action == Items.Player.EActions.DOUBLE))
                                {
                                    // Nastav hraca na stojaceho
                                    Program.myPlayer.Action = Items.Player.EActions.STAND;
                                }
                                else
                                {
                                    // Zavolaj menu akcii hry
                                    actionsMenu.InvokeResult().Wait();

                                    switch (Program.myPlayer.Action)
                                    {
                                        case Items.Player.EActions.HIT:
                                            {
                                                // Hrac taha 1 kartu
                                                client.UpdateItemAsync(new Uri(Program.serverUri, "blackjack/get-card"), Program.myPlayer).Wait();
                                                break;
                                            }
                                        case Items.Player.EActions.DOUBLE:
                                            {
                                                // Hrac taha 1 kartu
                                                client.UpdateItemAsync(new Uri(Program.serverUri, "blackjack/get-card"), Program.myPlayer).Wait();
                                                // Zdvojnasobi stavku
                                                Program.myPlayer.Bet <<= 1;
                                                break;
                                            }
                                        case Items.Player.EActions.EXIT:
                                            {
                                                // Ak mam iba prve 2 karty mozem hru vzdat
                                                if (my[0].CardCount == 2)
                                                {
                                                    // Hrac ukoncil hru
                                                    Program.myPlayer.State = Items.Player.EState.FREE;

                                                    // Prisiel o polovicu stavky
                                                    Program.myPlayer.Wallet -= (Program.myPlayer.Bet >> 1);
                                                }
                                                break;
                                            }
                                    }                                
                                }

                                // Aktualizuj hraca
                                client.UpdateItemAsync(new Uri(Program.serverUri, "players/update"), Program.myPlayer).Wait();
                            }
                            // Inak hrac caka na ukoncenie kola (kym krupier dohra)
                            else
                            {
                                if (croupier.Action == Items.Player.EActions.STAND)
                                {
                                    // Krupierov sucet kariet
                                    var cSum = croupier.CardSum;

                                    // Sucet vyrovnany
                                    if (mySum == cSum)
                                    {
                                        // Nastav remizu hraca
                                        Program.myPlayer.State = Items.Player.EState.DRAW;
                                    }
                                    // Hrac ziskal Blackjack (eso + 10karta)
                                    else if ((my[0].CardCount == 2) && (mySum == 21))
                                    {
                                        // Nastav vyhru hraca
                                        Program.myPlayer.State = Items.Player.EState.WIN;

                                        // Vyhra vyplatena v pomere 3 : 2
                                        Program.myPlayer.Wallet += ((Program.myPlayer.Bet * 3) >> 1);
                                    }
                                    // Ak mam vyssie karty alebo krupier prehral
                                    else if ((cSum > 21) || (mySum > cSum))
                                    {
                                        // Nastav vyhru hraca
                                        Program.myPlayer.State = Items.Player.EState.WIN;

                                        // Ziskal vyhru
                                        Program.myPlayer.Wallet += Program.myPlayer.Bet;
                                    }
                                    // Krupier mal vyssie karty
                                    else
                                    {
                                        // Nastav prehru hraca
                                        Program.myPlayer.State = Items.Player.EState.LOSE;

                                        // Prisiel o stavku
                                        Program.myPlayer.Wallet -= Program.myPlayer.Bet;
                                    }

                                    // Aktualizuj hraca
                                    client.UpdateItemAsync(new Uri(Program.serverUri, "players/update"), Program.myPlayer).Wait();
                                }
                            }

                            Task.Delay(100).Wait();     // Refresh 60Hz => 16ms
                        }
                      // Ak hrac uz dohral ukonci cyklus
                    } while (true);

                    // Uvolnit hracove karty na serveri
                    client.DeleteItemAsync(new Uri(Program.serverUri, "blackjack/free-card"), Program.myPlayer).Wait();

                    Program.ShowWarning("Round ended");
                    Console.WriteLine("\n");
                }

                // Vymaz hraca od stolu
                Remove(client);
            }
        }

        // Vymaz hraca
        public override void Exit()
        {
            using (var client = new Client())
            {
                // Vymaz hraca od stolu
                Remove(client);
            }
        }

        // Titulna informacia hry
        private void Head()
        {
            Console.WriteLine("===============================================");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.BackgroundColor = ConsoleColor.Black;
            Console.Write("                    Blackjack                  ");
            Console.ResetColor();
            Console.WriteLine("\n===============================================\n");
        }

        // Nacitaj vysku stavky z klavesnice
        private long SetBet()
        {
            string betS;
            long bet;

            while (true)
            {
                Console.WriteLine("Enter q - quit the game");
                Console.Write("Enter bet [$]: ");
                betS = Console.ReadLine();

                // Zadajte q pre koniec hry
                if (betS.Contains('q') || betS.Contains('Q'))
                    return -1L;

                // Zadajte spravnu vysku stavky
                if (long.TryParse(betS, out bet))
                {
                    // Minimalna stanovena stavka v kasine je 100$
                    if (bet < 100L)
                    {
                        Program.ShowError("Minimal bet is 100$");
                        Console.WriteLine();
                    }
                    else
                    {
                        break;
                    }
                }                
            }

            Console.WriteLine();

            return bet;
        }

        // Vycisti hracov profil po ukonceni hry
        private void Remove(Client client)
        {
            // Vynuluj hru
            Program.myPlayer.Action = Items.Player.EActions.NONE;
            Program.myPlayer.Bet = 0L;
            Program.myPlayer.State = Items.Player.EState.FREE;
            Program.myPlayer.GameId = "Nothing";

            // Aktualizuj hraca
            client.UpdateItemAsync(new Uri(Program.serverUri, "players/update"), Program.myPlayer).Wait();
        }

        // Vypis stolu na konzolu
        private void Print(List<Items.Player> players, Items.Player croupier)
        {
            const int MAX_WIDTH = 25;
            int height;
            string sumS;

            Console.Clear();

            Console.CursorLeft = ((players.Count * MAX_WIDTH) >> 1) + ((MAX_WIDTH >> 1) - (croupier.Nickname.Length >> 1));
            Console.WriteLine($"{croupier.Nickname}");

            // Sucet kariet hraca
            sumS = croupier.CardSum.ToString();
            Console.CursorLeft = ((players.Count * MAX_WIDTH) >> 1) + ((MAX_WIDTH >> 1) - (sumS.Length >> 1));
            Console.WriteLine(sumS);

            Console.CursorLeft = ((players.Count * MAX_WIDTH) >> 1) + ((MAX_WIDTH >> 1) - (croupier.Cards.Count() << 1));
            foreach (var c in croupier.Cards)
            {
                c.Print();
                Console.Write(" ");
            }
            Console.Write("\n\n\n\n\n\n\n");


            height = 0;
            for (int i = 0; i < players.Count; i++)
            {
                // Karty hraca
                if (players[i].Cards.Any())
                {
                    foreach (var c in players[i].Cards)
                    {
                        Console.CursorLeft = (i * MAX_WIDTH) + (MAX_WIDTH >> 1) - 1;
                        c.Print();
                        Console.WriteLine();
                        height++;
                    }
                }

                // Vyska stavky hraca
                var betS = $"{players[i].Bet}$";
                Console.CursorLeft = (i * MAX_WIDTH) + ((MAX_WIDTH >> 1) - (betS.Length >> 1));
                Console.WriteLine(betS);
                height++;

                // Ak ide o mojho hraca vypis stav penazenky
                if (players[i].Nickname == Program.myPlayer.Nickname)
                {
                    var walletS = $"{Program.myPlayer.Wallet}$";
                    Console.CursorLeft = (i * MAX_WIDTH) + ((MAX_WIDTH >> 1) - (walletS.Length >> 1));
                    Console.WriteLine(walletS);
                    height++;
                }

                // Meno hraca
                Console.CursorLeft = (i * MAX_WIDTH) + ((MAX_WIDTH >> 1) - (players[i].Nickname.Length >> 1));
                Console.WriteLine(players[i].Nickname);
                height++;

                // Sucet kariet hraca
                sumS = players[i].CardSum.ToString();
                Console.CursorLeft = (i * MAX_WIDTH) + ((MAX_WIDTH >> 1) - (sumS.Length >> 1));
                Console.WriteLine(sumS);
                height++;

                // Vysledny stav hry
                var stateS = players[i].State.ToString();
                Console.CursorLeft = (i * MAX_WIDTH) + ((MAX_WIDTH >> 1) - (stateS.Length >> 1));
                switch (players[i].State)
                {
                    case Items.Player.EState.WIN:
                        {
                            Console.ForegroundColor = ConsoleColor.Green;
                            Console.BackgroundColor = ConsoleColor.Black;
                            Console.Write(stateS);
                            Console.ResetColor();
                            Console.WriteLine();
                            break;
                        }
                    case Items.Player.EState.LOSE:
                        {
                            Program.ShowError(stateS);
                            Console.WriteLine();
                            break;
                        }
                    case Items.Player.EState.DRAW:
                        {
                            Program.ShowWarning(stateS);
                            Console.WriteLine();
                            break;
                        }
                    default:
                        {
                            Console.WriteLine(stateS);
                            break;
                        }
                }
                height++;

                if (i < (players.Count - 1))
                {
                    Console.CursorTop -= height;
                    height = 0;
                }
            }

            Console.WriteLine("\n");
        }
    }
}
