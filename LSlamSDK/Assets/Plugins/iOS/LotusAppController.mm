#import <UIKit/UIKit.h>
#import "UnityAppController.h"
#import "UnityInterface.h"

extern "C" void  UnityPluginLoad(IUnityInterfaces* unityInterfaces);
extern "C" void  UnityPluginUnload();

@interface LotusAppController : UnityAppController
{
}
- (void)shouldAttachRenderDelegate;
@end

@implementation LotusAppController

- (void)shouldAttachRenderDelegate;
{
    UnityRegisterRenderingPluginV5(&UnityPluginLoad, &UnityPluginUnload);
}
@end


IMPL_APP_CONTROLLER_SUBCLASS(LotusAppController)
