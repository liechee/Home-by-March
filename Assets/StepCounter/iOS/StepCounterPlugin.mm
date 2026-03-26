#import <Foundation/Foundation.h>
#include <CoreMotion/CoreMotion.h>
#include <UIKit/UIKit.h>

class StepCounterPlugin {

public:

    typedef void (*OnDataReceived) (int callbackID, int numberOfSteps);
    typedef void (*OnPermissionResult) (bool granted);
    
    struct StepCounterCallbacks
    {
        OnDataReceived dataReceived;
        OnPermissionResult permissionResult;
    };
    
    struct StepCounterPermissionCallbacks {
        OnPermissionResult onPermissionResult;
    };

    StepCounterPlugin()
    {
        m_Pedometer = nullptr;
        m_Device = -1;
    }
    
    void enable() {
        if(m_Pedometer == nullptr) {
            m_Pedometer = [[CMPedometer alloc]init];
            m_Device = 1;
        }
    }
    
    bool isAuthorized() {
            if (![CMPedometer isStepCountingAvailable]) {
                // Pedometer is not available on this device.
                return false;
            }

            CMAuthorizationStatus status = [CMPedometer authorizationStatus];
            
            // CMAuthorizationStatusAuthorized means the app has permission.
            return status == CMAuthorizationStatusAuthorized;
    }
    
    bool isSubscribed() {
        return (m_Pedometer != nullptr);
    }
    
    void redirectToAppSettings() {
        // Check if the device is running iOS 8.0 or later
        if (@available(iOS 8.0, *)) {
            NSURL *settingsURL = [NSURL URLWithString:UIApplicationOpenSettingsURLString];
            if ([[UIApplication sharedApplication] canOpenURL:settingsURL]) {
                [[UIApplication sharedApplication] openURL:settingsURL options:@{} completionHandler:nil];
            } else {
                // Handle the case where the settings URL cannot be opened
                NSLog(@"Unable to open settings URL.");
            }
        } else {
            // Handle the case for iOS versions below 8.0 if needed
            NSLog(@"Settings URL is not available for iOS versions below 8.0.");
        }
    }
    
    void requestPermission(StepCounterPermissionCallbacks* callbacks){
        if ([CMPedometer isStepCountingAvailable]) {
            if(!isSubscribed()){
                enable();
            }
            m_PermissionCallbacks = *callbacks;
            // Request step count data from the pedometer
            [m_Pedometer queryPedometerDataFromDate: [NSDate date] toDate: [NSDate date] withHandler:^(CMPedometerData * _Nullable pedometerData, NSError * _Nullable error) {
                dispatch_async(dispatch_get_main_queue(), ^{
                    if (error) {
                        m_PermissionCallbacks.onPermissionResult(false);
                        return;
                    }
                    m_PermissionCallbacks.onPermissionResult(true);
                });
            }];
        } else {
            NSLog(@"Step counting is not available on this device.");
            m_PermissionCallbacks.onPermissionResult(false);
            return;
        }
    }

    void queryPedometerData(NSString *fromTime, NSString *toTime, StepCounterCallbacks* callbacks, int callbackID) {
        m_Callbacks = *callbacks;
        if(!isAuthorized()){
            m_Callbacks.permissionResult(false);
        }
        if ([CMPedometer isStepCountingAvailable]) {
            if(!isSubscribed()){
                enable();
            }
            // Define the start and end dates
            NSDateFormatter *dateFormat = [[NSDateFormatter alloc] init];
            [dateFormat setDateFormat:@"yyyy-MM-dd'T'HH:mm:ss.SSS"];
            NSDate *fromDate = [dateFormat dateFromString:fromTime];
            NSDate *toDate = [dateFormat dateFromString:toTime];
            if (fromDate == nil || toDate == nil) {
                NSLog(@"Error: date could not be created. The input string might be invalid.");
                return;
            }
            [m_Pedometer queryPedometerDataFromDate:fromDate toDate:toDate withHandler:^(CMPedometerData * _Nullable pedometerData, NSError * _Nullable error) {
                dispatch_async(dispatch_get_main_queue(), ^{
                    if (error) {
                        NSLog(@"Error fetching pedometer data: %@", error.localizedDescription);
                        return;
                    }
                    if (pedometerData) {
                        m_Callbacks.dataReceived(callbackID, pedometerData.numberOfSteps.intValue);
                    } else {
                        NSLog(@"No data");
                    }
                });
            }];
        } else {
            NSLog(@"Step counting is not available.");
        }
    }
    
private:
    CMPedometer* m_Pedometer;
    StepCounterCallbacks m_Callbacks;
    StepCounterPermissionCallbacks m_PermissionCallbacks;
    int m_Device;
};

static StepCounterPlugin plugin;

extern "C" {
    
    void _QuerySteps(const char *fromTime, const char *toTime, StepCounterPlugin::StepCounterCallbacks* callbacks, int callbackID) {
        plugin.queryPedometerData([NSString stringWithUTF8String:fromTime], [NSString stringWithUTF8String:toTime], callbacks, callbackID);
    }

    void _EnableStepCounter() {
        plugin.enable();
    }

    void _RequestStepCounterPermission(StepCounterPlugin::StepCounterPermissionCallbacks* callbacks) {
        plugin.requestPermission(callbacks);
    }

    bool _IsStepCounterAuthorized() {
        return plugin.isAuthorized();
    }

    bool _IsStepCounterSubscribed() {
        return plugin.isSubscribed();
    }
    
    bool _IsStepCountingAvailable() {
        return [CMPedometer isStepCountingAvailable];
    }

    void _RedirectToStepCounterSettings() {
        plugin.redirectToAppSettings();
    }
}
