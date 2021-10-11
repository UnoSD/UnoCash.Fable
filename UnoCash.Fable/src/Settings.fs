module UnoCash.Fable.Settings

open Fable.FontAwesome
open Fable.React.Props
open Fulma
open UnoCash.Fulma.Helpers
open Fable.React

type Currency =
    | GBP
    | EUR
    | THB

let private (|Icon|) =
    function
    | GBP -> Fa.Solid.PoundSign
    | EUR -> Fa.Solid.EuroSign
    | _   -> Fa.Solid.DollarSign

let private accounts =
    [
        ("Current", GBP)
        ("ISA"    , EUR)
        ("Wallet" , THB)
    ]

let private accountsPanel accounts =
    Columns.columns [ ]
        [ Column.column [ Column.Offset (Screen.All, Column.Is3)
                          Column.Width (Screen.All, Column.Is6) ]
            [ Panel.panel [ ]
                [ yield   Panel.heading [ ] [ str "Accounts"]
                  yield   Panel.Block.div [ ]
                            [ Control.div [ Control.HasIconLeft ]
                                [ Input.text [ Input.Size IsSmall
                                               Input.Placeholder "Account name" ]
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
                                            Button.IsFullWidth ]
                                          [ str "Add" ] ] ] ] ]

let settingsCard _ _ =
    let modifiers =
        Container.Modifiers [ Modifier.TextAlignment (Screen.All, TextAlignment.Centered) ]
        
    card [ Hero.body []
                     [ Container.container [ Container.IsFluid
                                             modifiers ]
                     [ accountsPanel accounts ] ] ] (str "")