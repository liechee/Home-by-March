using System;

public class StepEvents{
    
    public event Action onStepAdded;

    public void stepAdded(){

        if(onStepAdded != null){
            onStepAdded();
        }
    }
}