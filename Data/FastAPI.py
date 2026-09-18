
from fastapi 
import FastAPI
import uvicorn

app = FastAPI()

@app.get("/get-data")

 def getـhubـdata():
 
 #تجهيز البيانات و جلبها 
 return {"status": "Success", "data"}
 
  if ــــnameــــ == "ــــmainــــ":
     uvicorn.run(app, host= "128.0.0.1", port=8000)
