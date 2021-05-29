module UnoCash.Fulma.SplitExpense.View

open Fable.FontAwesome
open Fable.React
open Fable.React.Props
open Feliz
open Fulma
open UnoCash.Fulma.Messages
open UnoCash.Fulma.Helpers
open UnoCash.Fulma.Models
open UnoCash.Fulma.Config

let private buttons dispatch submitText =
    Card.footer []
                [ Card.Footer.a [ Props [ onClick AddNewExpense dispatch ] ]
                                [ str submitText ]
                  Card.Footer.a [ Props [ onClick (ChangeToTab SplitExpense) dispatch ] ]
                                [ str "Split" ] ]

let private expenseForm model dispatch =
    let descriptionField =
        Field.div [ ]
                  [ Label.label [ ] [ str "Description" ]
                    Control.div [ Control.IsLoading true ]
                                [ Textarea.textarea [ Textarea.Option.Value model.Expense.Description
                                                      Textarea.Props [ onChange ChangeDescription dispatch ] ] [] ] ]
    
    let simpleField labelText icon input =
        Field.div []
                  [ Label.label [] [ str labelText ]
                    Control.div [ Control.HasIconLeft ]
                                [ input
                                  Icon.icon [ Icon.Size IsSmall; Icon.IsLeft ]
                                            [ Fa.i [ icon ] [ ] ] ] ]
    
    let amountField =
        Input.number [ Input.Props [ Step "0.01"
                                     onChange ChangeAmount dispatch ]
                       Input.Value (string model.Expense.Amount) ] |>
        simpleField "Amount" Fa.Solid.PoundSign
    
    let addEmptyOnAlert empty element =
        match model.Alert with
        | DuplicateTag -> element
        | _            -> empty
    
    let addOnAlert element =
        addEmptyOnAlert Html.none element
    
    let tagsInputIcon =
        Icon.icon [ Icon.Size IsSmall; Icon.IsLeft ] [ Fa.i [ Fa.Solid.Tags ] [ ] ]
    
    let tagsInputText =
         [ Input.Props [ OnKeyDown (fun ev -> TagsKeyDown (ev.key, ev.Value) |> dispatch) ]
           Input.Placeholder "Ex: groceries"
           Input.Value model.TagsText
           Input.OnChange (fun ev -> TagsTextChanged ev.Value |> dispatch) ] @
         ([ Input.Color IsDanger ] |> addEmptyOnAlert []) |> Input.text
    
    let tagsField =
       let alertIcon =
           Icon.icon [ Icon.Size IsSmall
                       Icon.IsRight ]
                     [ Fa.i [ Fa.Solid.ExclamationTriangle ] [] ]
       
       let helpText =
           Help.help [ Help.Color IsDanger ] [ str "Duplicate tag" ]
             
       let control =
           [ tagsInputIcon
             tagsInputText
             addOnAlert alertIcon ] |>
           Control.div [ Control.HasIconLeft
                         Control.HasIconRight ]
             
       [ Label.label [] [ str "Tags" ]
         control
         addOnAlert helpText ] |>
       Field.div []
    
    let tags =
        let tagIcon name =
            Tag.tag [ Tag.Color IsInfo ] [ Icon.icon [ ] [ Fa.i [ tagIconLookup name ] [ ] ] ]
        
        let tagText name =
            Tag.tag [ Tag.Color IsLight ] [ str name ]
            
        let tagDeleteButton name =
            Tag.delete [ Tag.Props [ onClick (TagDelete name) dispatch ] ] [ ]    
        
        let tag name =
            Control.div []
                        [ Tag.list [ Tag.List.HasAddons ]
                                   [ tagText name
                                     tagIcon name
                                     tagDeleteButton name ] ]

        model.Expense.Tags |>
        List.map tag |>
        Field.div [ Field.IsGroupedMultiline ]
   
    form [ amountField
 
           tagsField
           
           tags

           descriptionField ]
                
let splitExpenseView model dispatch =
    card [ expenseForm model dispatch ]
         (buttons dispatch "Apply")