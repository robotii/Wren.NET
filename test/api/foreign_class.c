#include <stdio.h>
#include <string.h>

#include "foreign_class.h"

static int finalized = 0;

static void apiGC(WrenVM* vm)
{
  wrenCollectGarbage(vm);
}

static void apiFinalized(WrenVM* vm)
{
  wrenReturnDouble(vm, finalized);
}

static void counterAllocate(WrenVM* vm)
{
  double* value = (double*)wrenAllocateForeign(vm, sizeof(double));
  *value = 0;
}

static void counterIncrement(WrenVM* vm)
{
  double* value = (double*)wrenGetArgumentForeign(vm, 0);
  double increment = wrenGetArgumentDouble(vm, 1);

  *value += increment;
}

static void counterValue(WrenVM* vm)
{
  double value = *(double*)wrenGetArgumentForeign(vm, 0);
  wrenReturnDouble(vm, value);
}

static void pointAllocate(WrenVM* vm)
{
  double* coordinates = (double*)wrenAllocateForeign(vm, sizeof(double[3]));

  // This gets called by both constructors, so sniff the argument count to see
  // which one was invoked.
  if (wrenGetArgumentCount(vm) == 1)
  {
    coordinates[0] = 0.0;
    coordinates[1] = 0.0;
    coordinates[2] = 0.0;
  }
  else
  {
    coordinates[0] = wrenGetArgumentDouble(vm, 1);
    coordinates[1] = wrenGetArgumentDouble(vm, 2);
    coordinates[2] = wrenGetArgumentDouble(vm, 3);
  }
}

static void pointTranslate(WrenVM* vm)
{
  double* coordinates = (double*)wrenGetArgumentForeign(vm, 0);
  coordinates[0] += wrenGetArgumentDouble(vm, 1);
  coordinates[1] += wrenGetArgumentDouble(vm, 2);
  coordinates[2] += wrenGetArgumentDouble(vm, 3);
}

static void pointToString(WrenVM* vm)
{
  double* coordinates = (double*)wrenGetArgumentForeign(vm, 0);
  char result[100];
  sprintf(result, "(%g, %g, %g)",
      coordinates[0], coordinates[1], coordinates[2]);
  wrenReturnString(vm, result, (int)strlen(result));
}

static void resourceAllocate(WrenVM* vm)
{
  wrenAllocateForeign(vm, 0);
}

static void resourceFinalize(WrenVM* vm)
{
  finalized++;
}

WrenForeignMethodFn foreignClassBindMethod(const char* signature)
{
  if (strcmp(signature, "static Api.gc()") == 0) return apiGC;
  if (strcmp(signature, "static Api.finalized") == 0) return apiFinalized;
  if (strcmp(signature, "Counter.increment(_)") == 0) return counterIncrement;
  if (strcmp(signature, "Counter.value") == 0) return counterValue;
  if (strcmp(signature, "Point.translate(_,_,_)") == 0) return pointTranslate;
  if (strcmp(signature, "Point.toString") == 0) return pointToString;

  return NULL;
}

void foreignClassBindClass(
    const char* className, WrenForeignClassMethods* methods)
{
  if (strcmp(className, "Counter") == 0)
  {
    methods->allocate = counterAllocate;
    return;
  }

  if (strcmp(className, "Point") == 0)
  {
    methods->allocate = pointAllocate;
    return;
  }

  if (strcmp(className, "Resource") == 0)
  {
    methods->allocate = resourceAllocate;
    methods->finalize = resourceFinalize;
    return;
  }
}
