module Marksman.Symbols

open Ionide.LanguageServerProtocol.Types

open Marksman.Misc
open Marksman.Paths
open Marksman.Names
open Marksman.Cst
open Marksman.Workspace
open Marksman.Index

let headingToSymbolName (h: Node<Heading>) : string = $"H{h.data.level}: {Heading.name h.data}"

let tagToSymbolName (t: Node<Tag>) : string = $"Tag: {t.data.name.text}"

let tagToSymbolInfo (docUri: DocId) (t: Node<Tag>) : SymbolInformation =
    let name = tagToSymbolName t
    let kind = SymbolKind.String
    let location = { Uri = docUri.uri; Range = t.range }

    let sym =
        { Name = name
          Kind = kind
          Location = location
          ContainerName = None }

    sym

let headingToSymbolInfo (docUri: DocId) (h: Node<Heading>) : SymbolInformation =
    let name = headingToSymbolName h
    let kind = SymbolKind.String
    let location = { Uri = docUri.uri; Range = h.range }

    let sym =
        { Name = name
          Kind = kind
          Location = location
          ContainerName = None }

    sym

let rec headingToDocumentSymbol (isEmacs: bool) (h: Node<Heading>) : DocumentSymbol =
    let name = Heading.name h.data
    let kind = SymbolKind.String
    let range = h.data.scope
    let selectionRange = h.range

    let children =
        h.data.children
        |> Element.pickHeadings
        |> Array.map (headingToDocumentSymbol isEmacs)

    let children =
        if Array.isEmpty children then
            None
        else if isEmacs then
            // Emacs' imenu with consult/counsel/etc. doesn't allow selecting intermediate
            // nodes that have children. As a workaround we add a '.' this node.
            let thisHeading =
                { Name = "."
                  Detail = None
                  Kind = kind
                  Range = selectionRange
                  SelectionRange = selectionRange
                  Children = None }

            Some(Array.append [| thisHeading |] children)
        else
            Some children

    { Name = name
      Detail = None
      Kind = kind
      Range = range
      SelectionRange = selectionRange
      Children = children }

let docSymbols
    (hierarchy: bool)
    (isEmacs: bool)
    (doc: Doc)
    : U2<array<SymbolInformation>, array<DocumentSymbol>> =
    if hierarchy then
        let topLevelHeadings =
            Doc.cst doc |> Seq.collect (Element.asHeading >> Option.toList)

        topLevelHeadings
        |> Seq.map (headingToDocumentSymbol isEmacs)
        |> Array.ofSeq
        |> Second
    else
        let allHeadings = Doc.index >> Index.headings <| doc

        allHeadings
        |> Seq.map (headingToSymbolInfo (Doc.id doc))
        |> Array.ofSeq
        |> First

let workspaceSymbols (query: string) (ws: Workspace) : array<SymbolInformation> =
    seq {
        for folder in Workspace.folders ws do
            for doc in Folder.docs folder do
                let matchingHeadingSymbols =
                    Doc.index doc
                    |> Index.headings
                    |> Seq.filter (headingToSymbolName >> query.IsSubSequenceOf)
                    |> Seq.map (headingToSymbolInfo (Doc.id doc))

                let matchingTagSymbols =
                    Doc.index doc
                    |> Index.tags
                    |> Seq.filter (tagToSymbolName >> query.IsSubSequenceOf)
                    |> Seq.map (tagToSymbolInfo (Doc.id doc))

                yield! Seq.append matchingHeadingSymbols matchingTagSymbols
    }
    |> Array.ofSeq
