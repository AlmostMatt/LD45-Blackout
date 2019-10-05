﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

/**
 * Represents a logical sentence.
 * 
 * Examples:
 * Sentence s = new Sentence(Noun.Alice,Verb.Is,Noun.Blonde,Adverb.False);
 * Alice IS Blonde TRUE
 * Alice HAS Knife TRUE
 * Blonde HAS Knife FALSE
 * 
 * Ideally a group of sentences can either imply guilt or suspicion, or eliminate possibilities.
 * 
 */

public class Sentence
{
    public Noun Subject;
    public Verb Verb; // Maybe all verbs are functionally identical
    public Noun DirectObject;
    public Adverb Adverb;

    public Sentence(Noun subj, Verb verb, Noun obj, Adverb adv)
    {
        this.Subject = subj;
        this.Verb = verb;
        this.DirectObject = obj;
        this.Adverb = adv;
    }

    public override bool Equals(System.Object obj)
    {
        //Check for null and compare run-time types.
        if ((obj == null) || !this.GetType().Equals(obj.GetType()))
        {
            return false;
        }

        Sentence other = (Sentence)obj;
        return(
               (Verb == other.Verb)
            && (Adverb == other.Adverb)
            && (
                    (Subject == other.Subject && DirectObject == other.DirectObject)
                || (Subject == other.DirectObject && DirectObject == other.Subject)
            )
        );
    }

    // there's a warning about not overriding GetHashCode?

    public override string ToString()
    {
        // try to make some more human readable
        string[] words = new string[]
        {
            Subject.ToString(),
            Verb.ToString().ToLower(),
            DirectObject.ToString()
        };

        /*
         string[] words = new string[] {
            Subject.Type().ToString(),
            Subject.ToString(),
            Verb.ToString(),
            DirectObject.Type().ToString(),
            DirectObject.ToString(),
            Adverb.ToString(),
        };
        */
        return string.Join(" ", words);
    }
}
