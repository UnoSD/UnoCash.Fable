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
                                [ str "Add" ] ]

let private splitExpenseForm model dispatch =
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
           
let private tableHeader () =
    let allColumns =
        [
            "Amount"
            "Tags"
            "Description"
        ]
                
    let columns =
        allColumns |>
        List.map (fun columnName -> th [] [ str columnName ])
    
    let columnsWithActions =
        columns @ [ th [ Style [ Width "1%" ] ] [ str "Actions" ] ]
    
    thead []
          [ tr []
               columnsWithActions ]

let private row dispatch (expense : Expense) =
    let cellButton message icon =
        a [ onClick message dispatch
            Style [ PaddingLeft "10px"; PaddingRight "10px" ] ]
          [ Fa.i [ icon ] [] ]
          
    let deleteButton =
        cellButton (DeleteExpense expense.id) Fa.Solid.Trash
        
    let allCells =
        [
            expense.amount |> string
            expense.tags
            expense.description
        ]
        
    let cells =
        allCells |>
        List.map (fun cellContent -> td [] [ str cellContent ])
    
    let cellsWithActions =
        cells @ [ td [ Style [ WhiteSpace WhiteSpaceOptions.Nowrap ] ]
                     [ deleteButton ] ]
    
    tr [] cellsWithActions
           
let private tableBody expenses dispatch =
    expenses |>
    Array.map (row dispatch) |>
    tbody []
           
let private expensesTable model dispatch =
    Table.table [ Table.IsBordered
                  Table.IsFullWidth
                  Table.IsStriped ]
                [ tableHeader ()
                  tableBody model.Expenses dispatch ]

let splitExpenseView model dispatch =
    card [ str "Total: <EDITABLE TOTAL>"
           expensesTable model dispatch
           splitExpenseForm model dispatch
           str "Left to allocate: <TOTAL-ITEMS>" ]
         (buttons dispatch "Apply and back")