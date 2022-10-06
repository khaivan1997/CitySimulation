using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public interface DynamicVisitorBase
{
    public void VisitPedestrian(PedestrianController c, int index);
    public void VisitVehicle(VehicleController c, int index);
}

public interface DynamicElementBase
{
    public void Accept(DynamicVisitorBase v, int index);
}


