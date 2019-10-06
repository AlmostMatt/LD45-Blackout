﻿using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AppearanceIdentityClue : ClueGenerator
{
    public override bool MatchTypes(NounType t1, NounType t2)
    {
        return (t1 == NounType.HairColor && t2 == NounType.Identity) || (t1 == NounType.Identity && t2 == NounType.HairColor);
    }

    public override ClueItem GetItem(Noun n1, Noun n2)
    {
        // ensure n1 is the hair color
        if (n1.Type() == NounType.Identity)
        {
            Noun temp = n1;
            n1 = n2;
            n2 = temp;
        }

        string spriteName = "Photo";
        string description = "A photo of the victim and his " + n2 + ", who has " + n1 + " hair,";
        ClueItem item = new ClueItem(n1, n2, spriteName, description);
        return (item);
    }
}