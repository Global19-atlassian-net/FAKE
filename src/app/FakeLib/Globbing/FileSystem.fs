﻿namespace Fake

// Copied from https://github.com/colinbull/FSharp.Enterprise/blob/master/src/FSharp.Enterprise/FileSystem.fs
module FileSystem = 
    
    open System
    open System.IO
    open Fake

    let fullPathRelativeTo rootDir file = 
        match Path.IsPathRooted(file) with
        | true -> file
        | false -> 
             match rootDir with
             | Some(root) -> Path.Combine(root, file)
             | None -> Path.GetFullPath(file)
        |> fun x -> new FileInfo(x)

    let tryReadFile deserialiser (path:string) =
        if File.Exists(path)
        then Some (File.ReadAllText(path) |> deserialiser)
        else None

    let write serialiser (path:string,payload:'a) =
        File.WriteAllText(path, serialiser payload)
    
    module Search = 
        
        type internal T = 
            | Directory of string
            | Recursive
            | FilePattern of string
        
        let internal exists dir root = 
            let di = new DirectoryInfo(Path.Combine(root, dir))
            if di.Exists
            then Some di.FullName
            else None
        
        let rec internal buildPaths acc (input : T list) =
            match input with
            | [] -> acc
            | Directory(name) :: t -> 
                match List.tryPick (exists name) acc with
                | Some(dir) -> buildPaths [dir] t 
                | None -> [] 
            | Recursive :: t ->
                let dirs = 
                    Seq.collect (fun dir -> Directory.EnumerateDirectories(dir, "*", SearchOption.AllDirectories)) acc
                    |> Seq.toList
                buildPaths (acc @ dirs) t
            | FilePattern(pattern) :: t -> 
                Seq.collect (fun dir -> Directory.EnumerateFiles(dir, pattern)) acc
                |> Seq.toList
        
        let getBaseDir () = 
            let assembly = Reflection.Assembly.GetEntryAssembly()
            if assembly = null then FullName "." else Path.GetDirectoryName assembly.Location
         
        let search baseDir (input : string) =
            let filePattern = Path.GetFileName(input)
            input.Split([|'/';'\\'|], StringSplitOptions.RemoveEmptyEntries)
            |> Seq.map (function
                        | "**" -> Recursive
                        | a when a = filePattern -> FilePattern(a)
                        | a -> Directory(a))
            |> Seq.toList
            |> buildPaths [baseDir]

        let find pattern = search (getBaseDir()) pattern