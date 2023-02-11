module UnoCash.Fable.Settings

open Fable.FontAwesome
open Fable.React.Props
open Fulma
open UnoCash.Fulma.Helpers
open Fable.React
open UnoCash.Fulma.Messages
open UnoCash.Fulma.Models

type Currency =
    | GBP
    | EUR
    | THB

let private (|Icon|) =
    function
    | GBP -> Fa.Solid.PoundSign
    | EUR -> Fa.Solid.EuroSign
    | _   -> Fa.Solid.DollarSign

let private accounts model =
    List.map (fun accountName -> (accountName, GBP)) model.Accounts

let private accountsPanel accounts model dispatch =
    Columns.columns [ ]
        [ Column.column [ Column.Offset (Screen.All, Column.Is3)
                          Column.Width (Screen.All, Column.Is6) ]
            [ Panel.panel [ ]
                [ yield   Panel.heading [ ] [ str "Accounts"]
                  yield   Panel.Block.div [ ]
                            [ Control.div [ Control.HasIconLeft ]
                                [ Input.text [ Input.Size IsSmall
                                               Input.Placeholder "Account name"
                                               Input.Value model.AccountName
                                               Input.OnChange (fun ev -> AccountNameChanged ev.Value |> dispatch) ]
                                  Icon.icon [ Icon.Size IsSmall
                                              Icon.IsLeft ]
                                            [ i [ ClassName "fa fa-plus" ] [ ] ] ] ]

                  for name, Icon(currencyIcon) in accounts do
                      yield (Panel.Block.a [ ]
                                [ Panel.icon [ ] [ Fa.i [ currencyIcon ] [ ] ]
                                  str name ])
                    
                  yield Panel.Block.div [ ]
                          [ Button.button [ Button.Color IsPrimary
                                            Button.IsOutlined
                                            Button.IsFullWidth
                                            Button.OnClick (fun _ -> AddAccount model.AccountName |> dispatch) ]
                                          [ str "Add" ] ] ] ] ]

let settingsCard model dispatch =
    let modifiers =
        Container.Modifiers [ Modifier.TextAlignment (Screen.All, TextAlignment.Centered) ]
        
    card [ Hero.body []
                     [ Container.container [ Container.IsFluid
                                             modifiers ]
                     [ accountsPanel (accounts model) model dispatch ] ] ] (str "")