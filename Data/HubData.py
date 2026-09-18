
import pandas as pd

class DataHub:
    def ــinitــ(self):
    self.usersـdata = None
    self.salesـdata = None


    def loadـallـdata(self):
         """Load Data """
     
     try:
         self.usersـdata = pd.readـcsv('users.csv')
         self.salesـdata = pd.readـexcel('sales.xlsx')
         print("Success to Load Data")
        except Exception as e:
        print("Failed to Load Data")


     def getـcleanـusers(self):

        """Clean and Refind Data Users"""
       
        if self.usersـdata is not None:

    return self.usersـdata.dropna()
   return None
