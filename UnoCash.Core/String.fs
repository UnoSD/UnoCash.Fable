module System.String

let split delimiters (value : string) = value.Split delimiters

let join (values : string seq) = String.Join("", values)